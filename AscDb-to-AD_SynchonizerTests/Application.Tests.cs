using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Logging;
using Domain;
using Application;
using Application.Interfaces.Data;
using Domain.Exceptions;

namespace Application.Tests
{
    [TestFixture]
    public class SyncServiceTests
    {
        private Mock<ISourceRepository> _dbRepositoryMock;
        private Mock<ITargetRepository> _ldapRepositoryMock;
        private Mock<ILogger<SyncService>> _loggerMock;
        private SyncService _syncService;
        private Dictionary<string, string> _fieldMappings;

        [SetUp]
        public void Setup()
        {
            _dbRepositoryMock = new Mock<ISourceRepository>();
            _ldapRepositoryMock = new Mock<ITargetRepository>();
            _loggerMock = new Mock<ILogger<SyncService>>();

            _fieldMappings = new Dictionary<string, string>
            {
                { "givenName", "FirstName" },
                { "sn", "LastName" },
                { "mail", "Email" }
            };

            _syncService = new SyncService(
                _dbRepositoryMock.Object,
                _ldapRepositoryMock.Object,
                _loggerMock.Object,
                _fieldMappings,
                "SamAccountName");
        }

        [Test]
        public async Task SyncUsersAsync_ShouldLogError_WhenExceptionOccurs()
        {
            // Arrange
            _dbRepositoryMock.Setup(x => x.GetUsersFromSourceAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act & Assert
            var exception = Assert.ThrowsAsync<UserSynchronizationException>(() =>
                _syncService.SyncUsersAsync());

            Assert.That(exception.Message, Is.EqualTo("Failed to synchronize users."));
            Assert.That(exception.InnerException, Is.Not.Null);
            Assert.That(exception.InnerException.Message, Is.EqualTo("Test exception"));

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task SyncUsersAsync_ShouldLogWarning_WhenUserNotFoundInTarget()
        {
            // Arrange
            var testUser = new User { SamAccountName = "test.user" };

            _dbRepositoryMock.Setup(x => x.GetUsersFromSourceAsync())
                .ReturnsAsync(new List<User> { testUser });

            _ldapRepositoryMock.Setup(x => x.FindUserInTarget(It.IsAny<string>()))
                .Returns((User)null); 

            // Act
            await _syncService.SyncUsersAsync(dryRun: true);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("not found in target system")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task SyncUsersAsync_ShouldUpdateUser_WhenAttributesDiffer()
        {
            // Arrange
            var sourceUser = new User
            {
                SamAccountName = "test.user",
                FirstName = "NewName",
                LastName = "NewLastName",
                Email = "new@email.com"
            };

            var targetUser = new User
            {
                SamAccountName = "test.user",
                FirstName = "OldName",
                LastName = "OldLastName",
                Email = "old@email.com"
            };

            _dbRepositoryMock.Setup(x => x.GetUsersFromSourceAsync())
                .ReturnsAsync(new List<User> { sourceUser });
            _ldapRepositoryMock.Setup(x => x.FindUserInTarget(It.IsAny<string>()))
                .Returns(targetUser);

            // Act (dry run = false)
            await _syncService.SyncUsersAsync(dryRun: false);

            // Assert
            _ldapRepositoryMock.Verify(x => x.UpdateUserInTarget(sourceUser), Times.Once);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("needs update")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        [Test]
        public async Task SyncUsersAsync_ShouldNotUpdateUser_WhenAttributesAreSame()
        {
            // Arrange
            var sourceUser = new User
            {
                SamAccountName = "test.user",
                FirstName = "SameName",
                LastName = "SameLastName",
                Email = "same@email.com"
            };

            var targetUser = new User
            {
                SamAccountName = "test.user",
                FirstName = "SameName",
                LastName = "SameLastName",
                Email = "same@email.com"
            };

            _dbRepositoryMock.Setup(x => x.GetUsersFromSourceAsync())
                .ReturnsAsync(new List<User> { sourceUser });
            _ldapRepositoryMock.Setup(x => x.FindUserInTarget(It.IsAny<string>()))
                .Returns(targetUser);

            // Act
            await _syncService.SyncUsersAsync();

            // Assert
            _ldapRepositoryMock.Verify(x => x.UpdateUserInTarget(It.IsAny<User>()), Times.Never);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("no update required")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        [Test]
        public void NeedUpdate_ShouldReturnTrue_WhenPropertiesDiffer()
        {
            //Arrange
            var source = new User { SamAccountName = "Same", FirstName = "New" };
            var target = new User { SamAccountName = "Same", FirstName = "Old" };

            //Act
            var result = _syncService.NeedUpdate(source, target);

            //Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void NeedUpdate_ShouldReturnFalse_WhenPropertiesAreSame()
        {
            //Arrange
            var source = new User { SamAccountName = "Same", FirstName = "Same", LastName = "Same" };
            var target = new User { SamAccountName = "Same", FirstName = "Same", LastName = "Same" };

            //Act
            var result = _syncService.NeedUpdate(source, target);

            //Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void GetIdentifier_ShouldReturnCorrectPropertyValue()
        {
            //Arrange
            var user = new User { SamAccountName = "test.user" };

            //Act
            var result = _syncService.GetIdentifier(user);

            //Assert
            Assert.AreEqual("test.user", result);
        }

        [Test]
        public void GetIdentifier_ShouldReturnEmptyString_WhenPropertyNotFound()
        {
            // Arrange
            var syncService = new SyncService(
                _dbRepositoryMock.Object,
                _ldapRepositoryMock.Object,
                _loggerMock.Object,
                _fieldMappings,
                "NonExistingProperty"); 

            var user = new User { SamAccountName = "test.user" };

            // Act
            var result = syncService.GetIdentifier(user);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void GetIdentifier_ShouldReturnEmptyString_WhenPropertyValueIsNull()
        {
            // Arrange
            var user = new User { SamAccountName = null }; 

            // Act
            var result = _syncService.GetIdentifier(user);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }
    }
}