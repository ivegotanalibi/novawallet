using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaWallet.Ledger.Api.Contracts;
using NovaWallet.Ledger.Application.Commands;
using NovaWallet.Ledger.Application.DTOs;
using NovaWallet.Ledger.Application.Queries;

namespace NovaWallet.Ledger.Api.Controllers;

[ApiController]
[Route("api/wallets")]
[Authorize]
[Produces("application/json", "application/problem+json")]
public class WalletsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WalletsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// POST /api/wallets — Create a new wallet for a customer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WalletDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> CreateWallet([FromBody] CreateWalletRequest request)
    {
        var result = await _mediator.Send(new CreateWalletCommand(request.CustomerId));
        return CreatedAtAction(nameof(GetBalance), new { walletId = result.WalletId }, result);
    }

    /// <summary>
    /// GET /api/wallets/{walletId}/balance — Get current balance.
    /// </summary>
    [HttpGet("{walletId:guid}/balance")]
    [ProducesResponseType(typeof(BalanceDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetBalance(Guid walletId)
    {
        var result = await _mediator.Send(new GetBalanceQuery(walletId));
        return Ok(result);
    }

    /// <summary>
    /// POST /api/wallets/{walletId}/credit — Deposit funds (simulates inbound NIP transfer).
    /// </summary>
    [HttpPost("{walletId:guid}/credit")]
    [ProducesResponseType(typeof(CreditResultDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> CreditWallet(Guid walletId, [FromBody] CreditRequest request)
    {
        var result = await _mediator.Send(new CreditWalletCommand(walletId, request.AmountKobo, request.Reference));
        return Ok(result);
    }

    /// <summary>
    /// POST /api/wallets/{walletId}/transfer — Move funds to another wallet.
    /// Accepts Idempotency-Key header for safe retries.
    /// </summary>
    [HttpPost("{walletId:guid}/transfer")]
    [ProducesResponseType(typeof(TransferResultDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    public async Task<IActionResult> Transfer(
        Guid walletId,
        [FromBody] TransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
    {
        var result = await _mediator.Send(new TransferCommand(
            walletId, request.ToWalletId, request.AmountKobo, request.Reference));
        return Ok(result);
    }

    /// <summary>
    /// GET /api/wallets/{walletId}/statement — Paginated transaction history, newest first.
    /// </summary>
    [HttpGet("{walletId:guid}/statement")]
    [ProducesResponseType(typeof(PaginatedStatementDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetStatement(
        Guid walletId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetStatementQuery(walletId, page, pageSize));
        return Ok(result);
    }
}
