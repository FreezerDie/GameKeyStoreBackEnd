using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameKeyStore.Services;
using GameKeyStore.Models;
using GameKeyStore.Authorization;
using System.Security.Claims;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Authorize] // Cart operations require authentication
    public class CartController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly EmailService _emailService;
        private readonly ILogger<CartController> _logger;

        public CartController(SupabaseService supabaseService, EmailService emailService, ILogger<CartController> logger)
        {
            _supabaseService = supabaseService;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's cart items with game and game key details
        /// </summary>
        /// <param name="includeDetails">Whether to include game and game key details (default: true)</param>
        /// <returns>List of cart items with game and game key details</returns>
        [HttpGet("api/cart/items")]
        public async Task<IActionResult> GetCartItems([FromQuery] bool includeDetails = true)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Fetch cart items for current user
                var cartItemsResponse = await client
                    .From<CartItem>()
                    .Where(x => x.UserId == userId)
                    .Order(x => x.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                
                var cartItems = cartItemsResponse.Models ?? new List<CartItem>();
                
                if (includeDetails && cartItems.Any())
                {
                    // Get all unique game IDs and game key IDs
                    var gameIds = cartItems
                        .Where(ci => ci.GameId.HasValue)
                        .Select(ci => ci.GameId!.Value)
                        .Distinct()
                        .ToList();
                    
                    var gameKeyIds = cartItems
                        .Where(ci => ci.GameKeyId.HasValue)
                        .Select(ci => ci.GameKeyId!.Value)
                        .Distinct()
                        .ToList();
                    
                    // Fetch only the relevant games and game keys
                    var games = new Dictionary<long, Game>();
                    var gameKeys = new Dictionary<long, GameKey>();
                    
                    if (gameIds.Any())
                    {
                        var gamesResponse = await client
                            .From<Game>()
                            .Filter("id", Supabase.Postgrest.Constants.Operator.In, gameIds)
                            .Get();
                        games = (gamesResponse.Models ?? new List<Game>()).ToDictionary(game => game.Id);
                    }
                    
                    if (gameKeyIds.Any())
                    {
                        var gameKeysResponse = await client
                            .From<GameKey>()
                            .Filter("id", Supabase.Postgrest.Constants.Operator.In, gameKeyIds)
                            .Get();
                        gameKeys = (gameKeysResponse.Models ?? new List<GameKey>()).ToDictionary(gk => gk.Id);
                    }
                    
                    // Create extended DTOs with details
                    var cartItemsWithDetails = cartItems.Select(cartItem => 
                    {
                        var cartItemWithDetails = new CartItemWithDetailsDto
                        {
                            Id = cartItem.Id,
                            CreatedAt = cartItem.CreatedAt,
                            GameId = cartItem.GameId,
                            GameKeyId = cartItem.GameKeyId,
                            UserId = cartItem.UserId,
                            Game = cartItem.GameId.HasValue && games.ContainsKey(cartItem.GameId.Value)
                                ? games[cartItem.GameId.Value].ToDto()
                                : null,
                            GameKey = cartItem.GameKeyId.HasValue && gameKeys.ContainsKey(cartItem.GameKeyId.Value)
                                ? gameKeys[cartItem.GameKeyId.Value].ToDto()
                                : null
                        };
                        return cartItemWithDetails;
                    }).ToList();
                    
                    return Ok(new { 
                        message = "Cart items with details fetched successfully",
                        count = cartItemsWithDetails.Count,
                        data = cartItemsWithDetails
                    });
                }
                else
                {
                    // Convert to simple DTOs
                    var cartItemDtos = cartItems.Select(x => x.ToDto()).ToList();
                    
                    return Ok(new { 
                        message = "Cart items fetched successfully",
                        count = cartItemDtos.Count,
                        data = cartItemDtos
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching cart items");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Add item to cart
        /// </summary>
        /// <param name="addToCartDto">Item to add to cart</param>
        /// <returns>Added cart item</returns>
        [HttpPost("api/cart/add")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto addToCartDto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // Validate that at least one of game_id or game_key_id is provided
                if (!addToCartDto.GameId.HasValue && !addToCartDto.GameKeyId.HasValue)
                {
                    return BadRequest(new { message = "Either game_id or game_key_id must be provided" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // If game_key_id is provided but game_id is not, fetch the game_id from game_key
                if (addToCartDto.GameKeyId.HasValue && !addToCartDto.GameId.HasValue)
                {
                    var gameKeyResponse = await client
                        .From<GameKey>()
                        .Where(x => x.Id == addToCartDto.GameKeyId.Value)
                        .Get();
                    
                    var gameKey = gameKeyResponse.Models?.FirstOrDefault();
                    
                    if (gameKey == null)
                    {
                        return BadRequest(new { message = "Game key not found" });
                    }
                    
                    if (!gameKey.GameId.HasValue)
                    {
                        return BadRequest(new { message = "Game key is not associated with any game" });
                    }
                    
                    // Set the game_id from the game_key
                    addToCartDto.GameId = gameKey.GameId.Value;
                }
                
                // Check if item already exists in cart
                var query = client
                    .From<CartItem>()
                    .Where(x => x.UserId == userId);
                
                // Add GameId condition only if it's provided
                if (addToCartDto.GameId.HasValue)
                {
                    query = query.Where(x => x.GameId == addToCartDto.GameId);
                }
                else
                {
                    query = query.Where(x => x.GameId == null);
                }
                
                // Add GameKeyId condition only if it's provided
                if (addToCartDto.GameKeyId.HasValue)
                {
                    query = query.Where(x => x.GameKeyId == addToCartDto.GameKeyId);
                }
                else
                {
                    query = query.Where(x => x.GameKeyId == null);
                }
                
                var existingCartItemResponse = await query.Get();
                
                var existingCartItem = existingCartItemResponse.Models?.FirstOrDefault();
                
                if (existingCartItem != null)
                {
                    return BadRequest(new { message = "Item already exists in cart" });
                }
                
                // Create new cart item
                var newCartItem = new CartItem
                {
                    UserId = userId,
                    GameId = addToCartDto.GameId,
                    GameKeyId = addToCartDto.GameKeyId,
                    CreatedAt = DateTime.UtcNow
                };
                
                var insertResponse = await client
                    .From<CartItem>()
                    .Insert(newCartItem);
                
                var insertedCartItem = insertResponse.Models?.FirstOrDefault();
                
                if (insertedCartItem != null)
                {
                    _logger.LogInformation("Cart item added successfully for user {UserId}", userId);
                    return Ok(new { 
                        message = "Item added to cart successfully", 
                        data = insertedCartItem.ToDto()
                    });
                }
                
                return BadRequest(new { message = "Failed to add item to cart" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        /// <param name="id">Cart item ID to remove</param>
        /// <returns>Success message</returns>
        [HttpDelete("api/cart/remove/{id}")]
        public async Task<IActionResult> RemoveFromCart(long id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // First verify the cart item exists and belongs to the current user
                var cartItemResponse = await client
                    .From<CartItem>()
                    .Where(x => x.Id == id && x.UserId == userId)
                    .Get();
                
                var cartItem = cartItemResponse.Models?.FirstOrDefault();
                
                if (cartItem == null)
                {
                    return NotFound(new { message = "Cart item not found or doesn't belong to current user" });
                }
                
                // Delete the cart item
                await client
                    .From<CartItem>()
                    .Where(x => x.Id == id && x.UserId == userId)
                    .Delete();
                
                _logger.LogInformation("Cart item {Id} removed successfully for user {UserId}", id, userId);
                return Ok(new { message = "Item removed from cart successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Clear all items from current user's cart
        /// </summary>
        /// <returns>Success message</returns>
        [HttpDelete("api/cart/clear")]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Get count of items before deletion for logging
                var cartItemsResponse = await client
                    .From<CartItem>()
                    .Where(x => x.UserId == userId)
                    .Get();
                
                var cartItemsCount = cartItemsResponse.Models?.Count ?? 0;
                
                // Delete all cart items for current user
                await client
                    .From<CartItem>()
                    .Where(x => x.UserId == userId)
                    .Delete();
                
                _logger.LogInformation("Cart cleared successfully for user {UserId}, removed {Count} items", userId, cartItemsCount);
                return Ok(new { 
                    message = "Cart cleared successfully",
                    itemsRemoved = cartItemsCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get cart item count for current user
        /// </summary>
        /// <returns>Cart item count</returns>
        [HttpGet("api/cart/count")]
        public async Task<IActionResult> GetCartItemCount()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                var cartItemsResponse = await client
                    .From<CartItem>()
                    .Where(x => x.UserId == userId)
                    .Get();
                
                var count = cartItemsResponse.Models?.Count ?? 0;
                
                return Ok(new { 
                    message = "Cart item count fetched successfully",
                    count = count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching cart item count");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Checkout current user's cart items
        /// </summary>
        /// <param name="createOrderDto">Order details (optional comment)</param>
        /// <returns>Created order with sub-orders</returns>
        [HttpPost("api/cart/checkout")]
        public async Task<IActionResult> CheckoutCart([FromBody] CreateOrderDto? createOrderDto = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Get current cart items with details
                var cartItemsResponse = await client
                    .From<CartItem>()
                    .Where(x => x.UserId == userId)
                    .Get();
                
                var cartItems = cartItemsResponse.Models ?? new List<CartItem>();
                
                if (!cartItems.Any())
                {
                    return BadRequest(new { message = "Cart is empty" });
                }

                // Get game keys to calculate total price
                var gameKeyIds = cartItems
                    .Where(ci => ci.GameKeyId.HasValue)
                    .Select(ci => ci.GameKeyId!.Value)
                    .Distinct()
                    .ToList();
                
                var gameKeys = new Dictionary<long, GameKey>();
                if (gameKeyIds.Any())
                {
                    var gameKeysResponse = await client
                        .From<GameKey>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.In, gameKeyIds)
                        .Get();
                    gameKeys = (gameKeysResponse.Models ?? new List<GameKey>()).ToDictionary(gk => gk.Id);
                }

                // Calculate total price (convert from float to cents for storage)
                long totalPriceCents = 0;
                foreach (var cartItem in cartItems)
                {
                    if (cartItem.GameKeyId.HasValue && gameKeys.ContainsKey(cartItem.GameKeyId.Value))
                    {
                        var gameKey = gameKeys[cartItem.GameKeyId.Value];
                        if (gameKey.Price.HasValue)
                        {
                            totalPriceCents += (long)(gameKey.Price.Value * 100); // Convert to cents
                        }
                    }
                }

                // Create order
                var newOrder = new Order
                {
                    TotalPrice = totalPriceCents,
                    UserId = userId,
                    Status = "pending",
                    Comment = createOrderDto?.Comment
                };

                var orderResponse = await client
                    .From<Order>()
                    .Insert(newOrder);
                
                var createdOrder = orderResponse.Models?.FirstOrDefault();
                
                if (createdOrder == null)
                {
                    return StatusCode(500, new { message = "Failed to create order" });
                }

                // Create sub-orders from cart items
                var subOrders = new List<SubOrder>();
                foreach (var cartItem in cartItems)
                {
                    var subOrder = new SubOrder
                    {
                        OrderId = createdOrder.Id,
                        UserId = userId,
                        GameId = cartItem.GameId,
                        GameKeyId = cartItem.GameKeyId,
                        CreatedAt = DateTime.UtcNow
                    };
                    subOrders.Add(subOrder);
                }

                var subOrdersResponse = await client
                    .From<SubOrder>()
                    .Insert(subOrders);
                
                var createdSubOrders = subOrdersResponse.Models ?? new List<SubOrder>();

                // Clear cart after successful order creation
                await client
                    .From<CartItem>()
                    .Where(x => x.UserId == userId)
                    .Delete();

                // Prepare response with detailed sub-orders
                var games = new Dictionary<long, Game>();
                var gameIds = createdSubOrders
                    .Where(so => so.GameId.HasValue)
                    .Select(so => so.GameId!.Value)
                    .Distinct()
                    .ToList();

                if (gameIds.Any())
                {
                    var gamesResponse = await client
                        .From<Game>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.In, gameIds)
                        .Get();
                    games = (gamesResponse.Models ?? new List<Game>()).ToDictionary(g => g.Id);
                }

                var subOrdersWithDetails = createdSubOrders.Select(so =>
                {
                    var soDto = new SubOrderWithDetailsDto
                    {
                        Id = so.Id,
                        CreatedAt = so.CreatedAt,
                        OrderId = so.OrderId,
                        UserId = so.UserId,
                        GameId = so.GameId,
                        GameKeyId = so.GameKeyId,
                        Game = so.GameId.HasValue && games.ContainsKey(so.GameId.Value)
                            ? games[so.GameId.Value].ToDto()
                            : null,
                        GameKey = so.GameKeyId.HasValue && gameKeys.ContainsKey(so.GameKeyId.Value)
                            ? gameKeys[so.GameKeyId.Value].ToDto()
                            : null,
                        Price = so.GameKeyId.HasValue && gameKeys.ContainsKey(so.GameKeyId.Value)
                            ? gameKeys[so.GameKeyId.Value].Price
                            : null
                    };
                    return soDto;
                }).ToList();

                var orderWithSubOrders = new OrderWithSubOrdersDto
                {
                    Id = createdOrder.Id,
                    TotalPrice = createdOrder.TotalPrice,
                    UserId = createdOrder.UserId,
                    Status = createdOrder.Status,
                    Comment = createdOrder.Comment,
                    SubOrders = subOrdersWithDetails
                };

                _logger.LogInformation("Order {OrderId} created successfully for user {UserId} with {ItemCount} items, total: ${Total}", 
                    createdOrder.Id, userId, createdSubOrders.Count, totalPriceCents / 100.0);

                // Send order confirmation email
                try
                {
                    // Get user details for email
                    var userResponse = await client
                        .From<User>()
                        .Where(x => x.Id == userId)
                        .Get();
                    
                    var user = userResponse.Models?.FirstOrDefault();
                    
                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        var emailSent = await _emailService.SendOrderConfirmationEmailAsync(
                            user.Email, 
                            !string.IsNullOrEmpty(user.Name) ? user.Name : user.Username, 
                            orderWithSubOrders
                        );
                        
                        if (emailSent)
                        {
                            _logger.LogInformation("Order confirmation email sent successfully to {Email} for order {OrderId}", 
                                user.Email, createdOrder.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to send order confirmation email to {Email} for order {OrderId}", 
                                user.Email, createdOrder.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("User {UserId} not found or has no email address, skipping order confirmation email", userId);
                    }
                }
                catch (Exception emailEx)
                {
                    // Don't fail the entire checkout if email fails
                    _logger.LogError(emailEx, "Error sending order confirmation email for order {OrderId}", createdOrder.Id);
                }

                return Ok(new CheckoutResponseDto
                {
                    Order = orderWithSubOrders,
                    Message = "Checkout completed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during checkout");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get order details by ID (only for current user)
        /// </summary>
        /// <param name="id">Order ID</param>
        /// <returns>Order with sub-orders details</returns>
        [HttpGet("api/orders/{id}")]
        public async Task<IActionResult> GetOrderById(long id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Get order (verify it belongs to current user)
                var orderResponse = await client
                    .From<Order>()
                    .Where(x => x.Id == id && x.UserId == userId)
                    .Get();
                
                var order = orderResponse.Models?.FirstOrDefault();
                
                if (order == null)
                {
                    return NotFound(new { message = "Order not found or doesn't belong to current user" });
                }

                // Get sub-orders for this order
                var subOrdersResponse = await client
                    .From<SubOrder>()
                    .Where(x => x.OrderId == id)
                    .Get();
                
                var subOrders = subOrdersResponse.Models ?? new List<SubOrder>();

                // Get game and game key details
                var gameIds = subOrders
                    .Where(so => so.GameId.HasValue)
                    .Select(so => so.GameId!.Value)
                    .Distinct()
                    .ToList();
                
                var gameKeyIds = subOrders
                    .Where(so => so.GameKeyId.HasValue)
                    .Select(so => so.GameKeyId!.Value)
                    .Distinct()
                    .ToList();

                var games = new Dictionary<long, Game>();
                var gameKeys = new Dictionary<long, GameKey>();

                if (gameIds.Any())
                {
                    var gamesResponse = await client
                        .From<Game>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.In, gameIds)
                        .Get();
                    games = (gamesResponse.Models ?? new List<Game>()).ToDictionary(g => g.Id);
                }

                if (gameKeyIds.Any())
                {
                    var gameKeysResponse = await client
                        .From<GameKey>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.In, gameKeyIds)
                        .Get();
                    gameKeys = (gameKeysResponse.Models ?? new List<GameKey>()).ToDictionary(gk => gk.Id);
                }

                // Create detailed sub-orders
                var subOrdersWithDetails = subOrders.Select(so =>
                {
                    var soDto = new SubOrderWithDetailsDto
                    {
                        Id = so.Id,
                        CreatedAt = so.CreatedAt,
                        OrderId = so.OrderId,
                        UserId = so.UserId,
                        GameId = so.GameId,
                        GameKeyId = so.GameKeyId,
                        Game = so.GameId.HasValue && games.ContainsKey(so.GameId.Value)
                            ? games[so.GameId.Value].ToDto()
                            : null,
                        GameKey = so.GameKeyId.HasValue && gameKeys.ContainsKey(so.GameKeyId.Value)
                            ? gameKeys[so.GameKeyId.Value].ToDto()
                            : null,
                        Price = so.GameKeyId.HasValue && gameKeys.ContainsKey(so.GameKeyId.Value)
                            ? gameKeys[so.GameKeyId.Value].Price
                            : null
                    };
                    return soDto;
                }).ToList();

                var orderWithSubOrders = new OrderWithSubOrdersDto
                {
                    Id = order.Id,
                    TotalPrice = order.TotalPrice,
                    UserId = order.UserId,
                    Status = order.Status,
                    Comment = order.Comment,
                    SubOrders = subOrdersWithDetails
                };

                return Ok(new { 
                    message = "Order details fetched successfully",
                    data = orderWithSubOrders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching order details");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get all orders for current user
        /// </summary>
        /// <returns>List of orders for current user</returns>
        [HttpGet("api/orders")]
        public async Task<IActionResult> GetUserOrders()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (userIdClaim == null || !long.TryParse(userIdClaim, out long userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                await _supabaseService.InitializeAsync();
                var client = _supabaseService.GetClient();
                
                // Get orders for current user
                var ordersResponse = await client
                    .From<Order>()
                    .Where(x => x.UserId == userId)
                    .Order(x => x.Id, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                
                var orders = ordersResponse.Models ?? new List<Order>();
                var orderDtos = orders.Select(o => o.ToDto()).ToList();

                return Ok(new { 
                    message = "User orders fetched successfully",
                    count = orderDtos.Count,
                    data = orderDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user orders");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    error = ex.Message 
                });
            }
        }
    }
}
