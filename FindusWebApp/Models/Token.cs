using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FindusWebApp.Models
{
    public class Token
    {
        [Column(TypeName = "VARCHAR")]
        [StringLength(50)]
        [Key]
        public string RealmId { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int ScopeHash { get; set; }
    }
}