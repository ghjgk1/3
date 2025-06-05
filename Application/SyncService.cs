using Domain;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Application.Interfaces.Data;
using System.Reflection;
using Domain.Exceptions;

[assembly: InternalsVisibleTo("AscDb_to_AD_SynchonizerTests")]
namespace Application
{
    public class SyncService 
    {
        private readonly ISourceRepository _dbRepository;
        private readonly ITargetRepository _ldapRepository;
        private readonly ILogger<SyncService> _logger;
        private readonly Dictionary<string, string> _fieldMappings;
        private readonly string _searchBy;

        public SyncService(
            ISourceRepository dbRepository,
            ITargetRepository ldapRepository,
            ILogger<SyncService> logger,
            Dictionary<string, string> fieldMappings,
            string searchBy)
        {
            _dbRepository = dbRepository;
            _ldapRepository = ldapRepository;
            _logger = logger;
            _fieldMappings = fieldMappings;
            _searchBy = searchBy;
        }

        public virtual async Task SyncUsersAsync(bool dryRun = true)
        {
            try
            {
                var sourceUsers = await _dbRepository.GetUsersFromSourceAsync();
                _logger.LogInformation("Retrieved {UserCount} users from source database", sourceUsers.Count());

                foreach (var sourceUser in sourceUsers)
                {
                    var identifier = GetIdentifier(sourceUser);
                    var targetUser = _ldapRepository.FindUserInTarget(identifier);

                    if (targetUser == null)
                    {
                        _logger.LogWarning("User {Identifier} not found in target system", identifier);
                        continue;
                    }

                    if (NeedUpdate(sourceUser, targetUser))
                    {
                        _logger.LogInformation("User {Identifier} needs update", identifier);
                        if (!dryRun)
                        {
                            _ldapRepository.UpdateUserInTarget(sourceUser);
                        }
                    }
                    else _logger.LogInformation("User {Identifier} is up-to-date in AD, no update required", identifier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user synchronization");
                throw new UserSynchronizationException("Failed to synchronize users.", ex);
            }
        }

        internal string GetIdentifier(User user)
        {
            var property = typeof(User).GetProperty(_searchBy);
            return property?.GetValue(user)?.ToString() ?? string.Empty;
        }

        internal bool NeedUpdate(User source, User target)
        {
            return _fieldMappings
                .Select(mapping => mapping.Value)
                .Select(propertyName => typeof(User).GetProperty(propertyName))
                .OfType<PropertyInfo>()
                .Any(property =>
                    !Equals(property.GetValue(source), property.GetValue(target)));
        }
    }
}
