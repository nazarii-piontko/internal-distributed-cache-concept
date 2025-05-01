using Microsoft.EntityFrameworkCore;

namespace InternalDistributedCache.Service;

public class EmployeesDbContext : DbContext
{
    public DbSet<Department> Departments { get; set; }
    
    public DbSet<Employee> Employers { get; set; }
    
    public EmployeesDbContext(DbContextOptions<EmployeesDbContext> options)
        : base(options)
    {
    }
}