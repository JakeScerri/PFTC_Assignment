// Services/FirestoreService.cs
using Google.Cloud.Firestore;
using JakeScerriPFTC_Assignment.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JakeScerriPFTC_Assignment.Services
{
   public class FirestoreService
   {
       private readonly FirestoreDb _firestoreDb;
       private readonly string _usersCollection = "users";
       private readonly string _ticketArchiveCollection = "ticket-archives";
       private readonly ILogger<FirestoreService> _logger;

       public FirestoreService(IConfiguration configuration, ILogger<FirestoreService> logger)
       {
           string projectId = configuration["GoogleCloud:ProjectId"];
           _logger = logger;
           _logger.LogInformation($"Initializing FirestoreService with project: {projectId}");
           _firestoreDb = FirestoreDb.Create(projectId);
       }

       // Modified to properly handle role preservation
       public async Task<User> SaveUserAsync(string email, UserRole? requestedRole = null)
       {
           try
           {
               _logger.LogInformation($"Saving user: {email}");
       
               // Check if user already exists
               User existingUser = await GetUserByEmailAsync(email);
               
               // Determine the role to use - preserve existing role if no role specifically requested
               UserRole roleToUse = requestedRole ?? existingUser?.Role ?? UserRole.User;
               
               if (existingUser != null)
               {
                   _logger.LogInformation($"User {email} exists with role {existingUser.Role}");
           
                   // Only update role if specifically requested with a different value
                   if (existingUser.Role != roleToUse && requestedRole.HasValue)
                   {
                       _logger.LogInformation($"Updating user {email} role from {existingUser.Role} to {roleToUse}");
                       existingUser.Role = roleToUse;
                       await UpdateUserAsync(existingUser);
                   }
                   else
                   {
                       _logger.LogInformation($"User {email} role unchanged: {existingUser.Role}");
                   }
           
                   return existingUser;
               }

               // Create new user
               _logger.LogInformation($"Creating new user {email} with role {roleToUse}");
               var user = new User
               {
                   Email = email,
                   Role = roleToUse,
                   CreatedAt = DateTime.UtcNow
               };

               // Create a dictionary representation for Firestore
               var userData = new Dictionary<string, object>
               {
                   { "Email", user.Email },
                   { "Role", (int)user.Role },
                   { "CreatedAt", user.CreatedAt }
               };

               // Save to Firestore
               DocumentReference docRef = _firestoreDb.Collection(_usersCollection).Document(email);
               await docRef.SetAsync(userData);
       
               _logger.LogInformation($"User {email} created with role {roleToUse}");
               return user;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, $"Error saving user: {email}");
               throw;
           }
       }

       // Get user by email
       public async Task<User> GetUserByEmailAsync(string email)
       {
           try
           {
               _logger.LogInformation($"Getting user: {email}");
               DocumentReference docRef = _firestoreDb.Collection(_usersCollection).Document(email);
               DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
               
               if (snapshot.Exists)
               {
                   _logger.LogInformation($"User {email} found");
                   // Try to convert to User object
                   try 
                   {
                       return snapshot.ConvertTo<User>();
                   }
                   catch (Exception ex) 
                   {
                       _logger.LogWarning(ex, $"Error converting user: {email}");
                       
                       // Try manual conversion as fallback
                       var userData = snapshot.ToDictionary();
                       return new User 
                       {
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
               
               _logger.LogInformation($"User {email} not found");
               return null;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, $"Error getting user: {email}");
               throw;
           }
       }

       // Update user
       public async Task<bool> UpdateUserAsync(User user)
       {
           try
           {
               _logger.LogInformation($"Updating user: {user.Email}");
               
               // Create a dictionary representation for more reliable updating
               var userData = new Dictionary<string, object>
               {
                   { "Email", user.Email },
                   { "Role", (int)user.Role },
                   { "CreatedAt", user.CreatedAt }
               };
               
               DocumentReference docRef = _firestoreDb.Collection(_usersCollection).Document(user.Email);
               await docRef.SetAsync(userData);
               _logger.LogInformation($"User {user.Email} updated");
               return true;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, $"Error updating user: {user.Email}");
               throw;
           }
       }

       // AA2.1.d - Get technicians
       public async Task<List<User>> GetTechniciansAsync()
       {
           try
           {
               _logger.LogInformation("Getting technicians");
               Query query = _firestoreDb.Collection(_usersCollection)
                   .WhereEqualTo("Role", (int)UserRole.Technician);
               
               QuerySnapshot querySnapshot = await query.GetSnapshotAsync();
               
               var technicians = new List<User>();
               foreach (DocumentSnapshot documentSnapshot in querySnapshot.Documents)
               {
                   try 
                   {
                       technicians.Add(documentSnapshot.ConvertTo<User>());
                   }
                   catch (Exception ex) 
                   {
                       _logger.LogWarning(ex, $"Error converting technician: {documentSnapshot.Id}");
                       
                       // Manual conversion as fallback
                       var userData = documentSnapshot.ToDictionary();
                       technicians.Add(new User 
                       {
                           Email = documentSnapshot.Id,
                           Role = UserRole.Technician,
                           CreatedAt = userData.ContainsKey("CreatedAt") 
                               ? (DateTime)userData["CreatedAt"] 
                               : DateTime.UtcNow
                       });
                   }
               }
               
               _logger.LogInformation($"Found {technicians.Count} technicians");
               return technicians;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error getting technicians");
               throw;
           }
       }

       // Archive closed tickets
       public async Task ArchiveTicketAsync(Ticket ticket, string technicianEmail)
       {
           try
           {
               _logger.LogInformation($"Archiving ticket: {ticket.Id}");
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
               
               _logger.LogInformation($"Ticket {ticket.Id} archived, closed by {technicianEmail}");
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, $"Error archiving ticket: {ticket.Id}");
               throw;
           }
       }
   }
}