using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Moq;
using Newtonsoft.Json.Linq;
using Probate.Api.Controllers;
using Probate.Api.Infrastructure.Authentication;
using Probate.Api.Infrastructure.Options;
using Probate.Api.Services;
using Probate.Db.Models;

namespace Probate.Api.Tests.Integration;

public class SubmissionOwnershipIntegrationTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly DbContextOptions<ProbateDbContext> _dbOptions;
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();
    private readonly Guid _deletedId = Guid.NewGuid();

    public SubmissionOwnershipIntegrationTests()
    {
        var databaseName = Guid.NewGuid().ToString();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(env => env.EnvironmentName).Returns(Environments.Development);
        _dbOptions = new DbContextOptionsBuilder<ProbateDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        using var db = new ProbateDbContext(_dbOptions);
        foreach (
            var (id, username, deleted) in new[]
            {
                (_deletedId, "alice", true),
                (_bobId, "bob", false),
                (_aliceId, "alice", false),
            }
        )
        {
            var submission = new Submission
            {
                PublicId = id,
                CreatedBy = username,
                ApplicantName = username,
                Status = SubmissionStatus.Draft,
                SubmissionData = "{}",
                DeletedAt = deleted ? DateTime.UtcNow : null,
            };
            submission.StepDataEntries.Add(
                new StepData
                {
                    FormId = "step1",
                    FormVersion = "1",
                    Data = $"{{\"privateValue\":\"{username}-private\"}}",
                }
            );
            db.Submissions.Add(submission);
        }
        db.SaveChanges();

        _host = new HostBuilder()
            .ConfigureWebHost(web =>
                web.UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddDbContext<ProbateDbContext>(options =>
                            options.UseInMemoryDatabase(databaseName)
                        );
                        services.AddScoped<ISubmissionService, SubmissionService>();
                        services.AddScoped<IStepDataService, StepDataService>();
                        services.Configure<KeycloakOptions>(options =>
                        {
                            options.Authority = "https://keycloak.example";
                            options.Client = "probate";
                            options.Secret = "test-secret";
                        });
                        services.AddProbateAuthentication(
                            environment.Object,
                            new ConfigurationBuilder().Build()
                        );
                        services.PostConfigure<OpenIdConnectOptions>(
                            OpenIdConnectDefaults.AuthenticationScheme,
                            options =>
                            {
                                var oidcConfiguration = new OpenIdConnectConfiguration
                                {
                                    AuthorizationEndpoint = "https://keycloak.example/authorize",
                                };
                                options.Configuration = oidcConfiguration;
                                options.ConfigurationManager =
                                    new StaticConfigurationManager<OpenIdConnectConfiguration>(
                                        oidcConfiguration
                                    );
                            }
                        );
                        services
                            .AddAuthentication(options =>
                            {
                                options.DefaultAuthenticateScheme = "Test";
                            })
                            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                                "Test",
                                _ => { }
                            );
                        services.AddAuthorization();
                        services
                            .AddControllers()
                            .AddApplicationPart(typeof(SubmissionsController).Assembly)
                            .AddNewtonsoftJson();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    })
            )
            .Build();
        _host.Start();
        _client = _host.GetTestClient();
        _client.DefaultRequestHeaders.Add("Test-User", "alice");
    }

    public static TheoryData<string, string> Endpoints =>
        new()
        {
            { "GET", "" },
            { "POST", "/submit" },
            { "DELETE", "" },
            { "PUT", "/steps/step1" },
            { "GET", "/steps/step1" },
            { "GET", "/steps" },
            { "GET", "/steps/compiled" },
        };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task IdentifierRoutes_RejectForeignMissingAndDeletedWithoutDataOrWrites(
        string method,
        string suffix
    )
    {
        string? notFoundBody = null;
        foreach (var id in new[] { _bobId, Guid.NewGuid(), _deletedId })
        {
            using var response = await SendAsync(method, id, suffix);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("bob", body);
            notFoundBody ??= body;
            Assert.Equal(notFoundBody, body);
        }

        using var db = new ProbateDbContext(_dbOptions);
        var bob = await db
            .Submissions.Include(s => s.StepDataEntries)
            .SingleAsync(s => s.PublicId == _bobId);
        Assert.Null(bob.DeletedAt);
        Assert.Null(bob.LastUpdatedAt);
        Assert.Equal(SubmissionStatus.Draft, bob.Status);
        Assert.Equal("{}", bob.SubmissionData);
        Assert.Equal("{\"privateValue\":\"bob-private\"}", Assert.Single(bob.StepDataEntries).Data);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task IdentifierRoutes_RejectAnonymousAndMissingUserId(string method, string suffix)
    {
        foreach (var username in new string?[] { null, "missing-claim", " " })
        {
            _client.DefaultRequestHeaders.Remove("Test-User");
            if (username != null)
                _client.DefaultRequestHeaders.Add("Test-User", username);
            using var response = await SendAsync(method, _aliceId, suffix);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task IdentifierRoutes_AllowOwner(string method, string suffix)
    {
        using var response = await SendAsync(method, _aliceId, suffix);
        Assert.Equal(
            method == "DELETE" ? HttpStatusCode.NoContent : HttpStatusCode.OK,
            response.StatusCode
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("bob", body);
        if (method == "GET" && suffix.StartsWith("/steps"))
            Assert.Contains("alice-private", body);

        using var db = new ProbateDbContext(_dbOptions);
        var alice = await db
            .Submissions.Include(s => s.StepDataEntries)
            .SingleAsync(s => s.PublicId == _aliceId);
        Assert.Equal("alice-id", alice.CreatedByUserId);
        Assert.Equal("alice", alice.CreatedBy);
        if (method == "DELETE")
            Assert.NotNull(alice.DeletedAt);
        if (method == "POST")
        {
            Assert.Equal(SubmissionStatus.ReadyToPrint, alice.Status);
            Assert.Contains("alice-private", alice.SubmissionData);
        }
        if (method == "PUT")
            Assert.Equal(
                "{\"privateValue\":\"updated\"}",
                Assert.Single(alice.StepDataEntries).Data
            );
    }

    [Fact]
    public async Task ListAndDraft_UseAuthenticatedOwnerNotCallerInput()
    {
        using var list = await _client.GetAsync("/api/submissions?username=bob");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = JArray.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(_aliceId, Assert.Single(items)["publicId"]!.ToObject<Guid>());
        Assert.DoesNotContain("bob", items.ToString());

        using var draft = await _client.PostAsJsonAsync(
            "/api/submissions/draft",
            new { createdBy = "bob" }
        );
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var created = JObject.Parse(await draft.Content.ReadAsStringAsync());
        Assert.Null(created.Value<string>("createdBy"));
        Assert.Null(created["createdByUserId"]);
        Assert.Null(created["sub"]);
        var createdId = created["publicId"]!.ToObject<Guid>();
        using var db = new ProbateDbContext(_dbOptions);
        var stored = await db.Submissions.SingleAsync(s => s.PublicId == createdId);
        Assert.Equal("alice-id", stored.CreatedByUserId);
        Assert.Null(stored.CreatedBy);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task IdentifierRoutes_DuplicateLegacyPublicIds_OnlyAccessAuthorizedOwner(
        string method,
        string suffix
    )
    {
        using (var db = new ProbateDbContext(_dbOptions))
        {
            // The legacy migration gave all existing submissions the same empty GUID.
            foreach (
                var submission in await db.Submissions.Where(s => s.DeletedAt == null).ToListAsync()
            )
                submission.PublicId = Guid.Empty;
            await db.SaveChangesAsync();
        }

        using var response = await SendAsync(method, Guid.Empty, suffix);
        Assert.Equal(
            method == "DELETE" ? HttpStatusCode.NoContent : HttpStatusCode.OK,
            response.StatusCode
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("bob", body);
        if (method == "GET" && suffix.StartsWith("/steps"))
            Assert.Contains("alice-private", body);

        using var stored = new ProbateDbContext(_dbOptions);
        var alice = await stored
            .Submissions.Include(s => s.StepDataEntries)
            .SingleAsync(s => s.CreatedBy == "alice" && s.PublicId == Guid.Empty);
        if (method == "DELETE")
            Assert.NotNull(alice.DeletedAt);
        if (method == "POST")
        {
            Assert.Equal(SubmissionStatus.ReadyToPrint, alice.Status);
            Assert.Contains("alice-private", alice.SubmissionData);
        }
        if (method == "PUT")
            Assert.Equal(
                "{\"privateValue\":\"updated\"}",
                Assert.Single(alice.StepDataEntries).Data
            );
        var bob = await stored
            .Submissions.Include(s => s.StepDataEntries)
            .SingleAsync(s => s.CreatedBy == "bob");
        Assert.Null(bob.DeletedAt);
        Assert.Null(bob.LastUpdatedAt);
        Assert.Equal(SubmissionStatus.Draft, bob.Status);
        Assert.Equal("{}", bob.SubmissionData);
        Assert.Equal("{\"privateValue\":\"bob-private\"}", Assert.Single(bob.StepDataEntries).Data);
    }

    [Fact]
    public async Task Delete_DuplicateOwnerIdWithDeletedRowFirst_DeletesLiveSubmission()
    {
        DateTime? originalDeletedAt;
        int deletedRowId;
        using (var db = new ProbateDbContext(_dbOptions))
        {
            var deleted = await db.Submissions.SingleAsync(s => s.PublicId == _deletedId);
            deleted.PublicId = _aliceId;
            originalDeletedAt = deleted.DeletedAt;
            deletedRowId = deleted.Id;
            await db.SaveChangesAsync();
        }

        using var response = await SendAsync("DELETE", _aliceId, "");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var get = await SendAsync("GET", _aliceId, "");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        using var stored = new ProbateDbContext(_dbOptions);
        Assert.All(await stored.Submissions.Where(s => s.PublicId == _aliceId).ToListAsync(), s =>
            Assert.NotNull(s.DeletedAt)
        );
        Assert.Equal(
            originalDeletedAt,
            (await stored.Submissions.SingleAsync(s => s.Id == deletedRowId)).DeletedAt
        );
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task IdentifierRoutes_NonNullForeignUserIdNeverFallsBackToMatchingUsername(
        string method,
        string suffix
    )
    {
        using (var db = new ProbateDbContext(_dbOptions))
        {
            var row = await db.Submissions.SingleAsync(s => s.PublicId == _aliceId);
            row.CreatedByUserId = "other-id";
            await db.SaveChangesAsync();
        }
        using var response = await SendAsync(method, _aliceId, suffix);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var stored = new ProbateDbContext(_dbOptions);
        var submission = await stored.Submissions.SingleAsync(s => s.PublicId == _aliceId);
        Assert.Equal("other-id", submission.CreatedByUserId);
        Assert.Null(submission.LastUpdatedAt);
        Assert.Null(submission.DeletedAt);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task IdentifierRoutes_UserIdOwnerSurvivesUsernameChangeOrAbsence(
        string method,
        string suffix
    )
    {
        using (var db = new ProbateDbContext(_dbOptions))
        {
            var row = await db.Submissions.SingleAsync(s => s.PublicId == _aliceId);
            row.CreatedByUserId = "alice-id";
            await db.SaveChangesAsync();
        }
        SetIdentity("alice-id", method == "GET" ? null : "new-username");
        using var response = await SendAsync(method, _aliceId, suffix);
        Assert.Equal(
            method == "DELETE" ? HttpStatusCode.NoContent : HttpStatusCode.OK,
            response.StatusCode
        );
        Assert.DoesNotContain("alice-id", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("sub", "alice")]
    [InlineData("mapped", "alice")]
    [InlineData("sub", null)]
    [InlineData("mapped", null)]
    public async Task Draft_UsesSubOrMappedNameIdentifierAndDoesNotExposeOwnerId(
        string claim,
        string? username
    )
    {
        SetIdentity(claim == "sub" ? "subject-id" : null, username);
        _client.DefaultRequestHeaders.Add("Test-NameIdentifier", "mapped-id");
        using var response = await _client.PostAsJsonAsync(
            "/api/submissions/draft",
            new { createdBy = "bob", createdByUserId = "injected-id" }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        Assert.Null(json["createdByUserId"]);
        Assert.Null(json["sub"]);
        Assert.Null(json.Value<string>("createdBy"));
        using var stored = new ProbateDbContext(_dbOptions);
        var publicId = json["publicId"]!.ToObject<Guid>();
        var row = await stored.Submissions.SingleAsync(s => s.PublicId == publicId);
        Assert.Equal(claim == "sub" ? "subject-id" : "mapped-id", row.CreatedByUserId);
        Assert.Null(row.CreatedBy);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task ListAndDraft_RequireUserIdEvenWithLegacyUsername(string method)
    {
        SetIdentity(null, "alice");
        using var response =
            method == "GET"
                ? await _client.GetAsync("/api/submissions")
                : await _client.PostAsync("/api/submissions/draft", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousSubmissionApiRequestReturns401InsteadOfAuthenticationRedirect()
    {
        _client.DefaultRequestHeaders.Remove("Test-User");
        using var response = await _client.GetAsync("/api/submissions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ExplicitLoginStillUsesConfiguredOpenIdConnectChallenge()
    {
        _client.DefaultRequestHeaders.Remove("Test-User");
        using var response = await _client.GetAsync("/api/auth/login");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(
            "https://keycloak.example/authorize",
            response.Headers.Location?.ToString()
        );
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task IdentifierRoutes_MissingUsernameCannotClaimLegacyRow(
        string method,
        string suffix
    )
    {
        SetIdentity("alice-id", null);
        using var response = await SendAsync(method, _aliceId, suffix);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var stored = new ProbateDbContext(_dbOptions);
        Assert.Null(
            (await stored.Submissions.SingleAsync(s => s.PublicId == _aliceId)).CreatedByUserId
        );
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("other-id", HttpStatusCode.NotFound)]
    [InlineData("alice-id", HttpStatusCode.BadRequest)]
    public async Task InvalidBody_DoesNotBypassIdentityOrOwnershipChecks(
        string? subject,
        HttpStatusCode expected
    )
    {
        SetIdentity(subject, subject == "other-id" ? "other-name" : "alice");
        using var response = await _client.PutAsJsonAsync(
            $"/api/submissions/{_aliceId}/steps/step1",
            new { }
        );
        Assert.Equal(expected, response.StatusCode);
        using var stored = new ProbateDbContext(_dbOptions);
        Assert.Null(
            (await stored.Submissions.SingleAsync(s => s.PublicId == _aliceId)).CreatedByUserId
        );
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("other-id", HttpStatusCode.NotFound)]
    [InlineData("alice-id", HttpStatusCode.UnsupportedMediaType)]
    public async Task UnsupportedContentType_DoesNotBypassIdentityOrOwnershipChecks(
        string? subject,
        HttpStatusCode expected
    )
    {
        SetIdentity(subject, subject == "other-id" ? "other-name" : "alice");
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/submissions/{_aliceId}/steps/step1"
        )
        {
            Content = new StringContent("invalid", Encoding.UTF8, "text/plain"),
        };
        using var response = await _client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        using var stored = new ProbateDbContext(_dbOptions);
        Assert.Null(
            (await stored.Submissions.SingleAsync(s => s.PublicId == _aliceId)).CreatedByUserId
        );
    }

    private void SetIdentity(string? subject, string? username)
    {
        _client.DefaultRequestHeaders.Remove("Test-User");
        _client.DefaultRequestHeaders.Remove("Test-Subject");
        _client.DefaultRequestHeaders.Remove("Test-NameIdentifier");
        _client.DefaultRequestHeaders.Add("Test-User", username ?? "missing-username");
        _client.DefaultRequestHeaders.Add("Test-Subject", subject ?? "missing-sub");
    }

    private async Task<HttpResponseMessage> SendAsync(string method, Guid id, string suffix)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            $"/api/submissions/{id}{suffix}"
        );
        if (method == "PUT")
            request.Content = JsonContent.Create(
                new
                {
                    formId = "step1",
                    data = "{\"privateValue\":\"updated\"}",
                    formVersion = "1",
                }
            );
        return await _client.SendAsync(request);
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
    }

    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder
        )
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Test-User", out var username))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim>();
            if (username == "missing-claim")
                claims.Add(new Claim("preferred_username", "alice"));
            else if (username != "missing-username")
                claims.Add(new Claim("preferred_username", username.ToString()));

            if (Request.Headers.TryGetValue("Test-Subject", out var subject))
            {
                if (subject != "missing-sub" && !string.IsNullOrWhiteSpace(subject))
                    claims.Add(new Claim("sub", subject.ToString()));
            }
            else if (username != "missing-claim" && !string.IsNullOrWhiteSpace(username))
                claims.Add(new Claim("sub", username + "-id"));

            if (Request.Headers.TryGetValue("Test-NameIdentifier", out var mapped))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, mapped.ToString()));
            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(
                        new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)),
                        Scheme.Name
                    )
                )
            );
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Redirect("https://keycloak.example/authorize");
            return Task.CompletedTask;
        }
    }
}
