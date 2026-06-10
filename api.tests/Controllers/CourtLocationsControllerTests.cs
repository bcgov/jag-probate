using Microsoft.AspNetCore.Mvc;
using Moq;
using Probate.Api.Controllers;
using Probate.Api.Models;
using Probate.Api.Services;
using Xunit;

namespace Probate.Api.Tests.Controllers;

public class CourtLocationsControllerTests
{
    private readonly Mock<ICourtLocationService> _mockCourtLocationService;
    private readonly CourtLocationsController _controller;

    public CourtLocationsControllerTests()
    {
        _mockCourtLocationService = new Mock<ICourtLocationService>();
        _controller = new CourtLocationsController(_mockCourtLocationService.Object);
    }

    [Fact]
    public async Task GetCourtLocations_ReturnsOkResultWithCourtLocations()
    {
        // Arrange
        var expectedResult = new CourtLocationResult
        {
            Courts = new List<CourtLocationModel>
            {
                new CourtLocationModel
                {
                    Id = 1,
                    IdentifierCode = "4871",
                    Name = "Vancouver Law Courts",
                    Code = "VAN",
                    IsSupremeCourt = true,
                    Address = new CourtAddress
                    {
                        AddressLine1 = "800 Smithe Street",
                        CityName = "Vancouver",
                        ProvinceName = "British Columbia",
                        PostalCode = "V6Z 2E1",
                        CountryName = "Canada",
                    },
                },
                new CourtLocationModel
                {
                    Id = 2,
                    IdentifierCode = "3561",
                    Name = "Victoria Law Courts",
                    Code = "VIC",
                    IsSupremeCourt = true,
                    Address = new CourtAddress
                    {
                        AddressLine1 = "850 Burdett Avenue",
                        CityName = "Victoria",
                        ProvinceName = "British Columbia",
                        PostalCode = "V8W 1B4",
                        CountryName = "Canada",
                    },
                },
            },
        };

        _mockCourtLocationService
            .Setup(x => x.GetCourtLocationsAsync())
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetCourtLocations();

        // Assert
        var okResult = Assert.IsType<ActionResult<CourtLocationResult>>(result);
        var okObjectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var returnedValue = Assert.IsType<CourtLocationResult>(okObjectResult.Value);
        Assert.Equal(2, returnedValue.Courts.Count);
        Assert.Equal("Vancouver Law Courts", returnedValue.Courts[0].Name);
        Assert.Equal("Victoria Law Courts", returnedValue.Courts[1].Name);
    }

    [Fact]
    public async Task GetCourtLocations_CallsServiceOnce()
    {
        // Arrange
        var expectedResult = new CourtLocationResult { Courts = new List<CourtLocationModel>() };

        _mockCourtLocationService
            .Setup(x => x.GetCourtLocationsAsync())
            .ReturnsAsync(expectedResult);

        // Act
        await _controller.GetCourtLocations();

        // Assert
        _mockCourtLocationService.Verify(x => x.GetCourtLocationsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetCourtLocations_ReturnsEmptyListWhenNoCourtLocations()
    {
        // Arrange
        var expectedResult = new CourtLocationResult { Courts = new List<CourtLocationModel>() };

        _mockCourtLocationService
            .Setup(x => x.GetCourtLocationsAsync())
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetCourtLocations();

        // Assert
        var okResult = Assert.IsType<ActionResult<CourtLocationResult>>(result);
        var okObjectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var returnedValue = Assert.IsType<CourtLocationResult>(okObjectResult.Value);
        Assert.Empty(returnedValue.Courts);
    }

    [Fact]
    public async Task GetCourtLocations_ReturnsCourtLocationsWithCompleteAddresses()
    {
        // Arrange
        var expectedResult = new CourtLocationResult
        {
            Courts = new List<CourtLocationModel>
            {
                new CourtLocationModel
                {
                    Id = 1,
                    IdentifierCode = "4871",
                    Name = "Vancouver Law Courts",
                    Code = "VAN",
                    IsSupremeCourt = true,
                    Address = new CourtAddress
                    {
                        AddressLine1 = "800 Smithe Street",
                        AddressLine2 = "Suite 100",
                        AddressLine3 = null,
                        CityName = "Vancouver",
                        ProvinceName = "British Columbia",
                        PostalCode = "V6Z 2E1",
                        CountryName = "Canada",
                    },
                },
            },
        };

        _mockCourtLocationService
            .Setup(x => x.GetCourtLocationsAsync())
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetCourtLocations();

        // Assert
        var okResult = Assert.IsType<ActionResult<CourtLocationResult>>(result);
        var okObjectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var returnedValue = Assert.IsType<CourtLocationResult>(okObjectResult.Value);
        var court = returnedValue.Courts[0];
        Assert.NotNull(court.Address);
        Assert.Equal("800 Smithe Street", court.Address.AddressLine1);
        Assert.Equal("Suite 100", court.Address.AddressLine2);
        Assert.Equal("Vancouver", court.Address.CityName);
        Assert.Equal("British Columbia", court.Address.ProvinceName);
        Assert.Equal("V6Z 2E1", court.Address.PostalCode);
        Assert.Equal("Canada", court.Address.CountryName);
    }

    [Fact]
    public async Task GetCourtLocations_FiltersSupremeAndProvincialCourts()
    {
        // Arrange
        var expectedResult = new CourtLocationResult
        {
            Courts = new List<CourtLocationModel>
            {
                new CourtLocationModel
                {
                    Id = 1,
                    Name = "Vancouver Supreme Court",
                    IsSupremeCourt = true,
                },
                new CourtLocationModel
                {
                    Id = 2,
                    Name = "Vancouver Provincial Court",
                    IsSupremeCourt = false,
                },
            },
        };

        _mockCourtLocationService
            .Setup(x => x.GetCourtLocationsAsync())
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetCourtLocations();

        // Assert
        var okResult = Assert.IsType<ActionResult<CourtLocationResult>>(result);
        var okObjectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var returnedValue = Assert.IsType<CourtLocationResult>(okObjectResult.Value);
        Assert.Equal(2, returnedValue.Courts.Count);
        Assert.Contains(returnedValue.Courts, c => c.IsSupremeCourt);
        Assert.Contains(returnedValue.Courts, c => !c.IsSupremeCourt);
    }
}
