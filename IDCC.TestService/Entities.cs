using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace InternalDistributedCache.Service;

[Index(nameof(Email), IsUnique = true)]
public sealed class Employee
{
    public long Id { get; set; }
    
    [MaxLength(255)]
    public string Email { get; set; } = null!;
    
    [MaxLength(255)]
    public string FullName { get; set; } = null!;
    
    public Department Department { get; set; } = null!;
    
    [ConcurrencyCheck]
    public long Version { get; set; }
}

[Index(nameof(Name), IsUnique = true)]
public sealed class Department
{
    public long Id { get; set; }
    
    [MaxLength(255)]
    public string Name { get; set; } = null!;
}
