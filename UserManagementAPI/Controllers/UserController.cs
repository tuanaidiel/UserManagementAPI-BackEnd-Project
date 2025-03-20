using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserManagementAPI.Models;

namespace UserManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private static List<User> users = new List<User>
        {
            new User { Id = 1, Name = "John Doe", Email = "john@example.com", Department = "HR" },
            new User { Id = 2, Name = "Jane Smith", Email = "jane@example.com", Department = "IT" }
        };
        
        private readonly ILogger<UsersController> _logger;
        
        public UsersController(ILogger<UsersController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public ActionResult<IEnumerable<User>> GetUsers()
        {
            try
            {
                _logger.LogInformation("Retrieved all users. Count: {Count}", users.Count);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "An error occurred while retrieving users");
            }
        }

        [HttpGet("{id}")]
        public ActionResult<User> GetUser(int id)
        {
            try
            {
                var user = users.FirstOrDefault(u => u.Id == id);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {Id} not found", id);
                    return NotFound($"User with ID {id} not found");
                }
                
                _logger.LogInformation("Retrieved user with ID {Id}", id);
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with ID {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the user");
            }
        }

        [HttpPost]
        public ActionResult<User> AddUser([FromBody] User user)
        {
            try
            {
                // Validate user data (ModelState validation will handle most of this)
                if (user == null)
                {
                    _logger.LogWarning("Invalid user data provided");
                    return BadRequest("User data is required");
                }
                
                // Check if user with same email already exists
                if (users.Any(u => u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Attempted to add user with duplicate email: {Email}", user.Email);
                    return BadRequest("A user with this email already exists");
                }
                
                // Set new ID
                user.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
                
                users.Add(user);
                _logger.LogInformation("Added new user with ID {Id}", user.Id);
                
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new user");
                return StatusCode(500, "An error occurred while adding the user");
            }
        }

        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, [FromBody] User updatedUser)
        {
            try
            {
                if (updatedUser == null)
                {
                    _logger.LogWarning("Invalid user data provided for update");
                    return BadRequest("User data is required");
                }
                
                var existingUser = users.FirstOrDefault(u => u.Id == id);
                if (existingUser == null)
                {
                    _logger.LogWarning("Attempted to update non-existent user with ID {Id}", id);
                    return NotFound($"User with ID {id} not found");
                }
                
                // Check if updated email conflicts with another user
                if (!string.Equals(existingUser.Email, updatedUser.Email, StringComparison.OrdinalIgnoreCase) &&
                    users.Any(u => u.Id != id && u.Email.Equals(updatedUser.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Attempted to update user with duplicate email: {Email}", updatedUser.Email);
                    return BadRequest("A user with this email already exists");
                }
                
                existingUser.Name = updatedUser.Name;
                existingUser.Email = updatedUser.Email;
                existingUser.Department = updatedUser.Department;
                
                _logger.LogInformation("Updated user with ID {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID {Id}", id);
                return StatusCode(500, "An error occurred while updating the user");
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                var user = users.FirstOrDefault(u => u.Id == id);
                if (user == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent user with ID {Id}", id);
                    return NotFound($"User with ID {id} not found");
                }
                
                users.Remove(user);
                _logger.LogInformation("Deleted user with ID {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID {Id}", id);
                return StatusCode(500, "An error occurred while deleting the user");
            }
        }
    }
}