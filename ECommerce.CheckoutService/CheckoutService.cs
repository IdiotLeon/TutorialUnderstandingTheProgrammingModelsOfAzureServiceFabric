using ECommerce.CheckoutService.Model;
using ECommerce.ProductCatalog.Model;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using UserActor.Interfaces;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerce.CheckoutService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class CheckoutService : StatefulService, ICheckoutService
    {
        public CheckoutService(StatefulServiceContext context)
            : base(context)
        { }

        public async Task<CheckoutSummary> Checkout(string userId)
        {
            var result = new CheckoutSummary
            {
                Date = DateTime.UtcNow,
                Products = new List<CheckoutProduct>()
            };

            // To call user actor to get the basket
            IUserActor userActor = GetUserActor(userId);
            Dictionary<Guid, int> basket = await userActor.GetBasket();

            // To get catalog client
            IProductCatalogService catalogService = GetProductCatalogService();

            // To contruct CheckoutProduct items by calling to the catalog
            foreach (KeyValuePair<Guid, int> basketLine in basket)
            {
                Product product = await catalogService.GetProduct(basketLine.Key);
                var checkoutProduct = new CheckoutProduct
                {
                    Product = product,
                    Price = product.Price,
                    Quantity = basketLine.Value
                };
                result.Products.Add(checkoutProduct);
            }

            // To generate total price
            result.TotalPrice = result.Products.Sum(p => p.Price);

            // To clear user basket
            await userActor.ClearBasket();

            await AddToHistory(result);

            return result;
        }

        public async Task<IEnumerable<CheckoutSummary>> GetOrderHistory(string userId)
        {
            var result = new List<CheckoutSummary>();
            var history = await StateManager.GetOrAddAsync<IReliableDictionary<DateTime, CheckoutSummary>>("history");

            using (var transaction = StateManager.CreateTransaction())
            {
                var allProducts = await history.CreateEnumerableAsync(transaction, EnumerationMode.Unordered);
                using (var enumerator = allProducts.GetAsyncEnumerator())
                {
                    while (await enumerator.MoveNextAsync(CancellationToken.None))
                    {
                        KeyValuePair<DateTime, CheckoutSummary> current = enumerator.Current;
                        result.Add(current.Value);
                    }
                }
            }

            return result;
        }

        private async Task AddToHistory(CheckoutSummary checkout)
        {
            var history = await StateManager.GetOrAddAsync<IReliableDictionary<DateTime, CheckoutSummary>>("history");

            using (var transaction = StateManager.CreateTransaction())
            {
                await history.AddAsync(transaction, checkout.Date, checkout);

                await transaction.CommitAsync();
            }
        }

        private IUserActor GetUserActor(string userId)
        {
            return ActorProxy.Create<IUserActor>(new ActorId(userId), new Uri("fabric:/ECommerce/UserActorService"));
        }

        private IProductCatalogService GetProductCatalogService()
        {
            return ServiceProxy.Create<IProductCatalogService>(
                new Uri("fabric:/ECommerce/ECommerce.ProductCatalog"),
                new ServicePartitionKey(0));
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[] {
                new ServiceReplicaListener(context => this.CreateServiceRemotingListener(context))
            };
        }
    }
}
