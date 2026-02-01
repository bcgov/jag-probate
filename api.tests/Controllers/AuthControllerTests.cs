using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Probate.Api.Controllers;
using Xunit;

namespace Probate.Api.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _controller = new AuthController(_mockConfiguration.Object);
        }

        [Fact]
        public void Login_WhenNotAuthenticated_ReturnsChallengeResult()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["Keycloak:KcIdpHint"]).Returns("bceid");

            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = _controller.Login("/dashboard");

            // Assert
            var challengeResult = Assert.IsType<ChallengeResult>(result);
            Assert.Contains(
                OpenIdConnectDefaults.AuthenticationScheme,
                challengeResult.AuthenticationSchemes
            );
            Assert.Equal("/dashboard", challengeResult.Properties?.RedirectUri);
            Assert.True(challengeResult.Properties?.IsPersistent);
        }

        [Fact]
        public void Login_WhenAlreadyAuthenticated_ReturnsRedirect()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.Name, "testuser") };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = claimsPrincipal };
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = _controller.Login("/dashboard");

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal("/dashboard", redirectResult.Url);
        }

        [Fact]
        public void GetAuthStatus_WhenNotAuthenticated_ReturnsNotAuthenticated()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = _controller.GetAuthStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic value = okResult.Value;
            Assert.False((bool)value.GetType().GetProperty("IsAuthenticated").GetValue(value));
        }

        [Fact]
        public void GetUserInfo_ReturnsUserClaimsWhenAuthenticated()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "John Doe"),
                new Claim(ClaimTypes.Email, "john@example.com"),
                new Claim(ClaimTypes.NameIdentifier, "user123"),
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = claimsPrincipal };
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = _controller.GetUserInfo();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }
    }
}
