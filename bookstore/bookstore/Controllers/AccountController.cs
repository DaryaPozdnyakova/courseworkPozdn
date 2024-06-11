using bookstore.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace bookstore.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError(string.Empty, "Пароли не совпадают.");
                    return View(model);
                }

                var user = new User
                {
                    Email = model.Email,
                    Password = HashPassword(model.Password),
                    PhoneNumber = model.PhoneNumber,
                    BirthDate = model.BirthDate,
                    FullName = model.FullName,
                    Avatar = ConvertToByteArray(model.Avatar)
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction("Login");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var hashedPassword = HashPassword(model.Password);
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == model.Email && u.Password == hashedPassword);
                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Email)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                    return RedirectToAction("Profile");
                }

                ModelState.AddModelError(string.Empty, "Неверная попытка входа.");
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users
                .Include(u => u.FavoriteBooks)
                .ThenInclude(fb => fb.Book)
                .SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Book)
                .ToListAsync();

            var profileViewModel = new ProfileViewModel
            {
                User = user,
                FavoriteBooks = user.FavoriteBooks.Select(fb => fb.Book).ToList(),
                Orders = orders
            };

            return View(profileViewModel);
        }

 
        public IActionResult UserCart()
        {
            List<int> cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            var cartItems = _context.Books.Where(b => cart.Contains(b.Id)).ToList();
            return View(cartItems);
        }

        [HttpPost]
        public IActionResult AddToCart(int id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { message = "Вы не авторизованы для добавления товаров в корзину." });
            }

            List<int> cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            if (!cart.Contains(id))
            {
                cart.Add(id);
                HttpContext.Session.SetObjectAsJson("Cart", cart);
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Товар добавлен в корзину." });
            }

            return RedirectToAction("Catalog", "Home");
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int id)
        {
            List<int> cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            cart.Remove(id);
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Товар удален из корзины." });
            }

            return RedirectToAction("UserCart");
        }

        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.SetObjectAsJson("Cart", new List<int>());
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(string address)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login");
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                return RedirectToAction("Login");
            }

            List<int> cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            if (!cart.Any())
            {
                return BadRequest("Корзина пуста.");
            }

            var order = new Order
            {
                UserId = userId,
                Address = address,
                OrderDate = DateTime.Now,
                OrderItems = new List<OrderItem>()
            };

            decimal totalPrice = 0;

            foreach (var bookId in cart)
            {
                var book = await _context.Books.FindAsync(bookId);
                if (book != null)
                {
                    order.OrderItems.Add(new OrderItem { BookId = bookId, Quantity = 1, Price = book.Price });
                    totalPrice += book.Price;
                }
            }

            order.TotalPrice = totalPrice;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

     
            HttpContext.Session.SetObjectAsJson("Cart", new List<int>());

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Book)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return NotFound("Заказ не найден.");
            }

            return View(order);
        }

 
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private byte[] ConvertToByteArray(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            using (var memoryStream = new MemoryStream())
            {
                file.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddToFavorites(int bookId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Вы не авторизованы" });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Json(new { success = false, message = "Вы не авторизованы" });
            }

            var userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users.Include(u => u.FavoriteBooks).SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Json(new { success = false, message = "Пользователь не найден" });
            }

            if (!user.FavoriteBooks.Any(fb => fb.BookId == bookId))
            {
                var favorite = new FavoriteBook
                {
                    UserId = userId,
                    BookId = bookId
                };

                _context.FavoriteBooks.Add(favorite);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }


        [HttpPost]
        public async Task<IActionResult> RemoveFromFavorites(int bookId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Вы не авторизованы" });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Json(new { success = false, message = "Вы не авторизованы" });
            }

            var userId = int.Parse(userIdClaim.Value);
            var favorite = await _context.FavoriteBooks.SingleOrDefaultAsync(fb => fb.UserId == userId && fb.BookId == bookId);

            if (favorite != null)
            {
                _context.FavoriteBooks.Remove(favorite);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Книга не найдена в избранном." });
        }




        [HttpGet]
        public async Task<IActionResult> Favorites()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login");
            }

            var userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users.Include(u => u.FavoriteBooks).ThenInclude(fb => fb.Book).SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var favoriteBooks = user.FavoriteBooks.Select(fb => fb.Book).ToList();

            return View(favoriteBooks);
        }

        [HttpPost]
        public IActionResult CancelOrder(int orderId)
        {
            var order = _context.Orders.Include(o => o.OrderItems).FirstOrDefault(o => o.Id == orderId);

            if (order == null)
            {
                return Json(new { success = false, message = "Заказ не найден." });
            }

           
            _context.OrderItems.RemoveRange(order.OrderItems);

         
            _context.Orders.Remove(order);

  
            _context.SaveChanges();

            return Json(new { success = true });
        }



        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login");
            }

            var userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            var model = new EditProfile
            {
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                BirthDate = user.BirthDate,
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile([Bind("UserId,Email,FullName,PhoneNumber,BirthDate")] EditProfile model)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login");
            }

            var userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                user.Email = model.Email;
                user.FullName = model.FullName;
                user.PhoneNumber = model.PhoneNumber;
                user.BirthDate = model.BirthDate;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return RedirectToAction("Profile");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeAvatar(IFormFile avatar)
        {
            if (avatar == null || avatar.Length == 0)
            {
                return BadRequest("Недопустимый файл.");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Вы не авторизованы.");
            }

            var userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("Пользователь не найден.");
            }

            using (var memoryStream = new MemoryStream())
            {
                await avatar.CopyToAsync(memoryStream);
                user.Avatar = memoryStream.ToArray();
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            var base64Avatar = Convert.ToBase64String(user.Avatar);
            var avatarUrl = $"data:image/png;base64,{base64Avatar}";

            return Json(new { avatarUrl });
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Пользователь не авторизован." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Json(new { success = false, message = "Не удалось получить идентификатор пользователя." });
            }

            var userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Json(new { success = false, message = "Пользователь не найден." });
            }

            if (user.Password != HashPassword(currentPassword))
            {
                return Json(new { success = false, message = "Текущий пароль неверен." });
            }

            if (newPassword != confirmPassword)
            {
                return Json(new { success = false, message = "Новый пароль и подтверждение нового пароля не совпадают." });
            }

            user.Password = HashPassword(newPassword);

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }



    }
}






