using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace bookstore.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string PhoneNumber { get; set; }
        [Column(TypeName = "date")] public DateTime? BirthDate { get; set; }
        public string FullName { get; set; }
        public byte[] Avatar { get; set; }

        public ICollection<FavoriteBook> FavoriteBooks { get; set; }
        public ICollection<Order> Orders { get; set; } 

    }
}
