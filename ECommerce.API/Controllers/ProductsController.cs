using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ECommerce.API.Model;

namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    public class ProductsController : Controller
    {
        // GET api/values
        [HttpGet]
        public async Task<IEnumerable<ApiProduct>> Get()
        {
            return new[] { new ApiProduct() { Id = Guid.NewGuid(), Description = "fake" } };
        }

        [HttpPost]
        public async Task Post([FromBody] ApiProduct product) {

        }
    }
}
