// Services/FirestoreService.cs
using Google.Cloud.Firestore;
using JakeScerriPFTC_Assignment.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JakeScerriPFTC_Assignment.Models;

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
            _firestoreDb = FirestoreDb.Create(projectId);
        }

        // AA2.1.d - Save user with role
        public async Task<User> SaveUserAsync(string email, UserRole role = UserRole.User)
        {
            try
            {
                // Check if user already exists
                User existingUser = await GetUserByEmailAsync(email);
                if (existingUser != null)
                {
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

                // Save to Firestore
                DocumentReference docRef = _firestoreDb.Collection(_usersCollection).Document(email);
                await docRef.SetAsync(user);
                
                Console.WriteLine($"User {email} created with role {role}");
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving user: {ex.Message}");
                throw;
            }
        }

        // Get user by email
        public async Task<User> GetUserByEmailAsync(string email)
        {
            try
            {
                DocumentReference docRef = _firestoreDb.Collection(_usersCollection).Document(email);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                
                if (snapshot.Exists)
                {
                    return snapshot.ConvertTo<User>();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user: {ex.Message}");
                throw;
            }
        }

        // Update user
        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                DocumentReference docRef = _firestoreDb.Collection(_usersCollection).Document(user.Email);
                await docRef.SetAsync(user);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user: {ex.Message}");
                throw;
            }
        }

        // AA2.1.d - Get technicians
        public async Task<List<User>> GetTechniciansAsync()
        {
            try
            {
                Query query = _firestoreDb.Collection(_usersCollection)
                    .WhereEqualTo("Role", UserRole.Technician);
                
                QuerySnapshot querySnapshot = await query.GetSnapshotAsync();
                
                var technicians = new List<User>();
                foreach (DocumentSnapshot documentSnapshot in querySnapshot.Documents)
                {
                    technicians.Add(documentSnapshot.ConvertTo<User>());
                }
                
                return technicians;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting technicians: {ex.Message}");
                throw;
            }
        }

        // Archive closed tickets
        public async Task ArchiveTicketAsync(Ticket ticket, string technicianEmail)
        {
            try
            {
                // Add technician info
                var ticketData = new Dictionary<string, object>
                {
                    { "Id", ticket.Id },
                    { "Title", ticket.Title },
                    { "Description", ticket.Description },
                    { "UserEmail", ticket.UserEmail },
                    { "DateUploaded", ticket.DateUploaded },
                    { "ImageUrls", ticket.ImageUrls },
                    { "Priority", ticket.Priority },
                    { "Status", TicketStatus.Closed },
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
                throw;
            }
        }
    }
}