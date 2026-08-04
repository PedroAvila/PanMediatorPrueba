using PanMediatorPrueba.Entities;

namespace PanMediatorPrueba.Repository;

public interface ICustomerRepository
{
    Task<int> CreateAsync(Customer customer);
}
