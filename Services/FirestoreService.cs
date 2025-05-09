// Services/FirestoreService.cs
using Google.Cloud.Firestore;
using JakeScerriPFTC_Assignment.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JakeScottPFTC_Assignment.Services
{
    public class FirestoreService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly string _usersCollection = "users";
        private readonly string _ticketArchiveCollection = "ticket-archives";

        public FirestoreService(IConfiguration configuration)
        {
            string projectId = configuration["GoogleCloud:ProjectId"];
            Console.WriteLine($"Initializing FirestoreService with project: {projectId}");
            _firestoreDb = FirestoreDb.Create(projectId);
        }

        // AA2.1.d - Save user with role
        public async Task<User> SaveUserAsync(string email, UserRole role = UserRole.User)
        {
            try
            {
                Console.WriteLine($"Attempting to save user: {email} with role: {role}");
                
                // Check if user already exists
                User existingUser = await GetUserByEmailAsync(email);
                if (existingUser != null)
                {
                    Console.WriteLine($"User {email} already exists");
                    // Update role if different
                    if (existingUser.Role != role)
                    {
                        existingUser.Role = role;
                        await UpdateUserAsync(existingUser);
                    }
                    return existingUser;
                }

                // Create new user
                var user = new User
                {
                    Email = email,
                    Role = role,
                    CreatedAt = DateTime.UtcNow
                };

                // Get a reference to the users collection
                CollectionReference colRef = _firestoreDb.Collection(_usersCollection);
                
                // Create a dictionary representation of the user for more reliable Firestore saving
                var userData = new Dictionary<string, object>
                {
                    { "Email", user.Email },
                    { "Role", (int)user.Role },
                    { "CreatedAt", user.CreatedAt }
                };

                // Save to Firestore
                DocumentReference docRef = colRef.Document(email);
                await docRef.SetAsync(userData);
                
                Console.WriteLine($"User {email} created with role {role}");
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving user: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Get user by email
        public async Task<User> GetUserByEmailAsync(string email)
        {
            try
            {
                Console.WriteLine($"Getting user: {email}");
                DocumentReference docRef = _firestoreDb.Collection(_usersCollection).Document(email);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                
                if (snapshot.Exists)
                {
                    Console.WriteLine($"User {email} found");
                    // Try to convert to User object
                    try {
                        return snapshot.ConvertTo<User>();
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"Error converting user: {ex.Message}");
                        
                        // Try manual conversion as fallback
                        var userData = snapshot.ToDictionary();
                        return new User {
                            Email = email,
                            Role = userData.ContainsKey("Role") 
                                ? (UserRole)Convert.ToInt32(userData["Role"]) 
                                : UserRole.User,
                            CreatedAt = userData.ContainsKey("CreatedAt") 
                                ? (DateTime)userData["CreatedAt"] 
                                : DateTime.UtcNow
                        };
                    }
                }
                
                Console.WriteLine($"User {email} not found");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Update user
        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                Console.WriteLine($"Updating user: {user.Email}");
                
                // Create a dictionary representation for more reliable updating
                var userData = new Dictionary<string, object>
                {
                    { "Email", user.Email },
                    { "Role", (int)user.Role },
                    { "CreatedAt", user.CreatedAt }
                };
                
                DocumentReference docRef = _firestoreDb.Collection(_usersCollection).Document(user.Email);
                await docRef.SetAsync(userData);
                Console.WriteLine($"User {user.Email} updated");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // AA2.1.d - Get technicians
        public async Task<List<User>> GetTechniciansAsync()
        {
            try
            {
                Console.WriteLine("Getting technicians");
                Query query = _firestoreDb.Collection(_usersCollection)
                    .WhereEqualTo("Role", (int)UserRole.Technician);
                
                QuerySnapshot querySnapshot = await query.GetSnapshotAsync();
                
                var technicians = new List<User>();
                foreach (DocumentSnapshot documentSnapshot in querySnapshot.Documents)
                {
                    try {
                        technicians.Add(documentSnapshot.ConvertTo<User>());
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"Error converting technician: {ex.Message}");
                        
                        // Manual conversion as fallback
                        var userData = documentSnapshot.ToDictionary();
                        technicians.Add(new User {
                            Email = documentSnapshot.Id,
                            Role = UserRole.Technician,
                            CreatedAt = userData.ContainsKey("CreatedAt") 
                                ? (DateTime)userData["CreatedAt"] 
                                : DateTime.UtcNow
                        });
                    }
                }
                
                Console.WriteLine($"Found {technicians.Count} technicians");
                return technicians;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting technicians: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Archive closed tickets
        public async Task ArchiveTicketAsync(Ticket ticket, string technicianEmail)
        {
            try
            {
                Console.WriteLine($"Archiving ticket: {ticket.Id}");
                // Add technician info
                var ticketData = new Dictionary<string, object>
                {
                    { "Id", ticket.Id },
                    { "Title", ticket.Title },
                    { "Description", ticket.Description },
                    { "UserEmail", ticket.UserEmail },
                    { "DateUploaded", ticket.DateUploaded },
                    { "ImageUrls", ticket.ImageUrls },
                    { "Priority", (int)ticket.Priority },
                    { "Status", (int)TicketStatus.Closed },
                    { "ClosedBy", technicianEmail },
                    { "ClosedAt", DateTime.UtcNow }
                };
                
                // Save to archive collection
                DocumentReference docRef = _firestoreDb.Collection(_ticketArchiveCollection).Document(ticket.Id);
                await docRef.SetAsync(ticketData);
                
                Console.WriteLine($"Ticket {ticket.Id} archived, closed by {technicianEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error archiving ticket: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}