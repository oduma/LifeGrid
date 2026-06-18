using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.UserSetup.Commands;
using NSubstitute;

namespace LifeGrid.Application.Tests.UserSetup;

public sealed class FactoryResetCommandTests
{
    private readonly IFactoryResetService         _service = Substitute.For<IFactoryResetService>();
    private readonly FactoryResetCommandHandler   _handler;

    public FactoryResetCommandTests()
        => _handler = new FactoryResetCommandHandler(_service);

    [Fact]
    public async Task DelegatesTo_IFactoryResetService()
    {
        await _handler.Handle(new FactoryResetCommand(), default);

        await _service.Received(1).ResetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsSuccess()
    {
        var result = await _handler.Handle(new FactoryResetCommand(), default);

        result.IsSuccess.Should().BeTrue();
    }
}
