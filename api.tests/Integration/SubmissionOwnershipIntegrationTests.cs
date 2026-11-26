using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Probate.Api.Controllers;
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
                        services
                            .AddAuthentication("Test")
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
    public async Task IdentifierRoutes_RejectAnonymousAndMissingUsername(
        string method,
        string suffix
    )
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
        Assert.Equal("alice", created.Value<string>("createdBy"));
        var createdId = created["publicId"]!.ToObject<Guid>();
        using var db = new ProbateDbContext(_dbOptions);
        Assert.Equal(
            "alice",
            (await db.Submissions.SingleAsync(s => s.PublicId == createdId)).CreatedBy
        );
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

            var claims =
                username == "missing-claim"
                    ? Array.Empty<Claim>()
                    : new[] { new Claim("preferred_username", username.ToString()) };
            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(
                        new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)),
                        Scheme.Name
                    )
                )
            );
        }
    }
}
