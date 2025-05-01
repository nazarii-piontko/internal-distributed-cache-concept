using Bogus;

namespace InternalDistributedCache.Service;

public sealed class EmployeesDbContextSeeder(EmployeesDbContext context)
{
    public void Seed(int employeesCount)
    {
        context.Database.EnsureCreated();
        
        if (!context.Departments.Any())
        {
            context.Departments.AddRange(
                new Department { Name = "HR" },
                new Department { Name = "IT" },
                new Department { Name = "Finance" },
                new Department { Name = "Marketing" },
                new Department { Name = "Sales" }
            );
            context.SaveChanges();
        }
        
        if (!context.Employers.Any())
        {
            var departments = context.Departments.ToList();
            var faker = new Faker<Employee>()
                .RuleFor(e => e.Email, f => f.Internet.Email())
                .RuleFor(e => e.FullName, f => f.Name.FullName())
                .RuleFor(e => e.Department, f => f.PickRandom(departments))
                .RuleFor(e => e.Version, f => 1);

            var employers = faker.Generate(employeesCount);
            context.Employers.AddRange(employers);
            context.SaveChanges();
        }
    }
}