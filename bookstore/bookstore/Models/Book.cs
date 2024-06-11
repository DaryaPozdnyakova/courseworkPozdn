namespace bookstore.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string NameAuthor { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public byte[] BookImage { get; set; }
        public ICollection<FavoriteBook> FavoriteBooks { get; set; }
        public ICollection<CartItem> CartItems { get; set; } 
        public ICollection<OrderItem> OrderItems { get; set; } 

    }

}
