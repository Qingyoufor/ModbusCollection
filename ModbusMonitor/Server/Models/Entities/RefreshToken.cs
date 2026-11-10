using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models.Entities;

/// <summary>刷新令牌（用于换取新 AccessToken，可作废）</summary>
[Table("RefreshTokens")]
[Index(nameof(Token),IsUnique =true)]   // Token 唯一索引，防重复/保证可定位
public class RefreshToken
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }                    // 主键

    public int UserId { get; set; }                // 用户外键
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }                // 所属用户

    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;  // 刷新令牌值

    public DateTime ExpiresAt { get; set; }        // 过期时间

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // 创建时间

    public DateTime? RevokedAt { get; set; }       // 作废时间（null=未作废）
}