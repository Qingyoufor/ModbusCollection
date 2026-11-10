using Microsoft.EntityFrameworkCore;
using Server.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models.Entities;

[Table("Users")]
[Index(nameof(Username), IsUnique = true)]   // 用户名唯一索引，防重复账号
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }                    // 主键

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;  // 用户名

    [Required]
    [MaxLength(100)]
    public string PasswordHash { get; set; } = string.Empty;  // 密码哈希


    public bool IsActive { get; set; } = true;      // 是否启用

    public DateTime? LastLoginAt { get; set; }      // 最后登录时间

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // 创建时间

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>(); // 该用户的刷新令牌
}

