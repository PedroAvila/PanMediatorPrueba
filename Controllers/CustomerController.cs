using Microsoft.AspNetCore.Mvc;
using PAN.Mediator.Abstractions;
using PanMediatorPrueba.Features.Customers;

namespace PanMediatorPrueba.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly IPanMediator _mediator;

        public CustomerController(IPanMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerCommand dto)
        {
            var result = await _mediator.Send(dto);
            return Created(string.Empty, result);
        }
    }
}
