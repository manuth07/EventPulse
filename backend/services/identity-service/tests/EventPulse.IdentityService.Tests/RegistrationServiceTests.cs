using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Services;

namespace EventPulse.IdentityService.Tests;

public class RegistrationServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<RoleManager<IdentityRole<Guid>>> _roleManagerMock;
    private readonly Mock<IEmailVerificationService> _emailVerificationServiceMock;
    private readonly Mock<ILogger<RegistrationService>> _loggerMock;
    private readonly RegistrationService _sut;

    public RegistrationServiceTests()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStoreMock = new Mock<IRoleStore<IdentityRole<Guid>>>();
        _roleManagerMock = new Mock<RoleManager<IdentityRole<Guid>>>(
            roleStoreMock.Object, null!, null!, null!, null!);

        _emailVerificationServiceMock = new Mock<IEmailVerificationService>();
        _loggerMock = new Mock<ILogger<RegistrationService>>();

        _sut = new RegistrationService(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _emailVerificationServiceMock.Object,
            _loggerMock.Object);
    }

    private static RegisterRequest ValidRequest() => new()
    {
        FirstName = "Nimal",
        LastName = "Perera",
        PhoneNumber = "0771234567",
        CountryCode = "LK",
        Email = "nimal.test@example.com",
        Password = "SecurePass123!"
    };

    // TC-AUTH-008 equivalent: duplicate email handling
    [Fact]
    public async Task RegisterCustomerAsync_WhenEmailAlreadyExists_ReturnsDuplicateResult()
    {
        var request = ValidRequest();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email.ToLowerInvariant()))
            .ReturnsAsync(new ApplicationUser { Email = request.Email });

        var result = await _sut.RegisterCustomerAsync(request);

        Assert.True(result.IsDuplicateEmail);
        Assert.False(result.Succeeded);
        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    // TC-AUTH-001 / TC-AUTH-027 equivalent: happy path + role assignment
    [Fact]
    public async Task RegisterCustomerAsync_WithValidData_CreatesUserAndAssignsCustomerRole()
    {
        var request = ValidRequest();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);
        _roleManagerMock
            .Setup(m => m.RoleExistsAsync(AppRoles.Customer))
            .ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Customer))
            .ReturnsAsync(IdentityResult.Success);
        _emailVerificationServiceMock
            .Setup(m => m.SendVerificationCodeAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var result = await _sut.RegisterCustomerAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(request.Email.ToLowerInvariant(), result.Response!.Email);
        Assert.True(result.Response.VerificationRequired);
        Assert.True(result.Response.VerificationEmailSent);
        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Customer),
            Times.Once);
    }

    // TC-AUTH-003 equivalent: password validation failure (delegated to Identity)
    [Fact]
    public async Task RegisterCustomerAsync_WhenPasswordFailsIdentityRules_ReturnsFailureWithErrors()
    {
        var request = ValidRequest();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        var identityError = new IdentityError { Description = "Passwords must be at least 8 characters." };
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        var result = await _sut.RegisterCustomerAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Passwords must be at least 8 characters.", result.Errors);
        _roleManagerMock.Verify(
            m => m.RoleExistsAsync(It.IsAny<string>()),
            Times.Never);
    }

    // Covers the rollback-on-role-assignment-failure branch (role already existed)
    [Fact]
    public async Task RegisterCustomerAsync_WhenRoleAssignmentFails_RollsBackCreatedUser()
    {
        var request = ValidRequest();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);
        _roleManagerMock
            .Setup(m => m.RoleExistsAsync(AppRoles.Customer))
            .ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Customer))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed." }));
        _userManagerMock
            .Setup(m => m.DeleteAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.RegisterCustomerAsync(request);

        Assert.False(result.Succeeded);
        _userManagerMock.Verify(
            m => m.DeleteAsync(It.IsAny<ApplicationUser>()),
            Times.Once);
    }

    // Covers the branch where the Customer role doesn't exist yet and must be created
    [Fact]
    public async Task RegisterCustomerAsync_WhenCustomerRoleDoesNotExist_CreatesRoleAndSucceeds()
    {
        var request = ValidRequest();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);
        _roleManagerMock
            .Setup(m => m.RoleExistsAsync(AppRoles.Customer))
            .ReturnsAsync(false); // role does not exist yet
        _roleManagerMock
            .Setup(m => m.CreateAsync(It.Is<IdentityRole<Guid>>(r => r.Name == AppRoles.Customer)))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Customer))
            .ReturnsAsync(IdentityResult.Success);
        _emailVerificationServiceMock
            .Setup(m => m.SendVerificationCodeAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var result = await _sut.RegisterCustomerAsync(request);

        Assert.True(result.Succeeded);
        _roleManagerMock.Verify(
            m => m.CreateAsync(It.Is<IdentityRole<Guid>>(r => r.Name == AppRoles.Customer)),
            Times.Once); // confirms the role was actually created
        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Customer),
            Times.Once); // confirms flow continued to role assignment afterward
    }

    // Covers the rollback branch when role CREATION (not assignment) fails
    [Fact]
    public async Task RegisterCustomerAsync_WhenRoleCreationFails_RollsBackCreatedUser()
    {
        var request = ValidRequest();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);
        _roleManagerMock
            .Setup(m => m.RoleExistsAsync(AppRoles.Customer))
            .ReturnsAsync(false);
        _roleManagerMock
            .Setup(m => m.CreateAsync(It.Is<IdentityRole<Guid>>(r => r.Name == AppRoles.Customer)))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role creation failed." }));
        _userManagerMock
            .Setup(m => m.DeleteAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.RegisterCustomerAsync(request);

        Assert.False(result.Succeeded);
        _userManagerMock.Verify(
            m => m.DeleteAsync(It.IsAny<ApplicationUser>()),
            Times.Once); // confirms rollback happened on role-creation failure specifically
        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never); // must never attempt role assignment if creation failed
    }

    // Confirms registration succeeds even when the verification email fails —
    // matches the real-world SMTP failure behavior observed in BUG-04.
    // The real SmtpEmailSender re-throws on delivery failure (see its catch block),
    // which is what RegistrationService's try/catch is actually built to handle —
    // so we simulate failure via an exception, not a `false` return
    // (a `false` return from SendVerificationCodeAsync means "resend cooldown active",
    // a different condition entirely — see EmailVerificationService.cs).
    [Fact]
    public async Task RegisterCustomerAsync_WhenEmailSendingFails_StillReturnsSuccessButFlagsEmailNotSent()
    {
        var request = ValidRequest();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);
        _roleManagerMock
            .Setup(m => m.RoleExistsAsync(AppRoles.Customer))
            .ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Customer))
            .ReturnsAsync(IdentityResult.Success);
        _emailVerificationServiceMock
            .Setup(m => m.SendVerificationCodeAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMTP connection failed"));

        var result = await _sut.RegisterCustomerAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(result.Response!.VerificationEmailSent);
    }
}