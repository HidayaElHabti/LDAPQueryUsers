using System.DirectoryServices;

namespace LdapUserSearch
{
    class Program
    {
        public static string LdapCs = "LDAP:/DC=**,DC=**";
        public static string? AdUsername = "***";
        public static string? AdPassword = "***";
        static void Main(string[] args)
        {
            Console.WriteLine("=== Active Directory User Search ===\n");

            while (true)
            {
                Console.WriteLine("\nChoose search option:");
                Console.WriteLine("1. Search by Username (sAMAccountName)");
                Console.WriteLine("2. Search by Email Address");
                Console.WriteLine("3. Search by Display Name");
                Console.WriteLine("4. Exit");
                Console.Write("Enter your choice (1-4): ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        SearchByUsername();
                        break;
                    case "2":
                        SearchByEmail();
                        break;
                    case "3":
                        SearchByDisplayName();
                        break;
                    case "4":
                        Console.WriteLine("Exiting application...");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }

        static void SearchByUsername()
        {
            Console.Write("Enter username to search: ");
            string username = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("Username cannot be empty.");
                return;
            }

            SearchUsers($"(sAMAccountName={EscapeLdapFilter(username)})", "Username");
        }

        static void SearchByEmail()
        {
            Console.Write("Enter email address to search: ");
            string email = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(email))
            {
                Console.WriteLine("Email cannot be empty.");
                return;
            }

            SearchUsers($"(mail={EscapeLdapFilter(email)})", "Email");
        }

        static void SearchByDisplayName()
        {
            Console.Write("Enter display name to search: ");
            string displayName = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(displayName))
            {
                Console.WriteLine("Display name cannot be empty.");
                return;
            }

            SearchUsers($"(displayName={EscapeLdapFilter(displayName)})", "Display Name");
        }

        static void SearchUsers(string filter, string searchType)
        {
            Console.WriteLine($"\nSearching by {searchType}...");
            Console.WriteLine("=" + new string('=', 50));

            try
            {
                // Build the complete LDAP filter for user objects
                string completeFilter = $"(&(objectCategory=person)(objectClass=user){filter})";

                using System.DirectoryServices.DirectoryEntry entry = new(LdapCs, AdUsername, AdPassword);
                using DirectorySearcher searcher = new(entry);
                searcher.Filter = completeFilter;

                // Load all relevant user properties
                /*searcher.PropertiesToLoad.AddRange(new string[] {
                        "sAMAccountName",        // Username
                        "displayName",           // Full name
                        "mail",                  // Email
                        "givenName",             // First name
                        "sn",                    // Last name
                        "title",                 // Job title
                        "department",            // Department
                        "telephoneNumber",       // Phone number
                        "manager",               // Manager
                        "whenCreated",           // Account creation date
                        "lastLogon",             // Last logon
                        "userAccountControl",    // Account status
                        "distinguishedName",     // DN
                        "memberOf"               // Groups
                    });*/

                //searcher.PageSize = 1000; // Enable paging for large results

                SearchResultCollection results = searcher.FindAll();

                if (results.Count == 0)
                {
                    Console.WriteLine("No users found matching your criteria.");
                    return;
                }

                Console.WriteLine($"Found {results.Count} user(s):\n");

                foreach (SearchResult result in results)
                {
                    Console.WriteLine(result.ToString());
                    Console.WriteLine(new string('-', 60));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching Active Directory: {ex.Message}");
                Console.WriteLine("Please check:");
                Console.WriteLine("1. You're connected to the domain network/VPN");
                Console.WriteLine("2. You have proper permissions");
                Console.WriteLine("3. The domain controller is accessible");
            }
        }

        static void DisplayUserDetails(SearchResult result)
        {
            // Basic information
            Console.WriteLine($"Username: {GetPropertyValue(result, "sAMAccountName")}");
            Console.WriteLine($"Display Name: {GetPropertyValue(result, "displayName")}");
            Console.WriteLine($"Email: {GetPropertyValue(result, "mail")}");
            Console.WriteLine($"First Name: {GetPropertyValue(result, "givenName")}");
            Console.WriteLine($"Last Name: {GetPropertyValue(result, "sn")}");

            // Job information
            Console.WriteLine($"Title: {GetPropertyValue(result, "title")}");
            Console.WriteLine($"Department: {GetPropertyValue(result, "department")}");
            Console.WriteLine($"Phone: {GetPropertyValue(result, "telephoneNumber")}");

            // Account information
            string accountStatus = IsAccountActive(result) ? "Active" : "Disabled";
            Console.WriteLine($"Account Status: {accountStatus}");

            // Manager
            string managerDN = GetPropertyValue(result, "manager");
            if (!string.IsNullOrEmpty(managerDN))
            {
                string managerName = GetDisplayNameFromDN(managerDN);
                Console.WriteLine($"Manager: {managerName}");
            }

            // Groups (first 5 for brevity)
            var groups = result.Properties["memberOf"];
            if (groups.Count > 0)
            {
                Console.WriteLine($"Group Memberships ({groups.Count} total):");
                for (int i = 0; i < Math.Min(5, groups.Count); i++)
                {
                    string groupDN = groups[i].ToString();
                    string groupName = GetCNFromDN(groupDN);
                    Console.WriteLine($"  - {groupName}");
                }
                if (groups.Count > 5)
                {
                    Console.WriteLine($"  ... and {groups.Count - 5} more groups");
                }
            }
        }

        static string GetPropertyValue(SearchResult result, string propertyName)
        {
            if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
            {
                return result.Properties[propertyName][0].ToString();
            }
            return "N/A";
        }

        static bool IsAccountActive(SearchResult result)
        {
            if (result.Properties.Contains("userAccountControl") &&
                result.Properties["userAccountControl"].Count > 0)
            {
                int userAccountControl = Convert.ToInt32(result.Properties["userAccountControl"][0]);
                // Check if account is disabled (bit 2)
                return (userAccountControl & 0x0002) == 0;
            }
            return true;
        }

        static string GetDisplayNameFromDN(string distinguishedName)
        {
            try
            {
                using (System.DirectoryServices.DirectoryEntry entry = new ($"{LdapCs}/{distinguishedName}"))
                {
                    if (entry.Properties["displayName"].Count > 0)
                        return entry.Properties["displayName"][0].ToString();
                    else if (entry.Properties["cn"].Count > 0)
                        return entry.Properties["cn"][0].ToString();
                }
            }
            catch
            {
                // If we can't resolve the DN, just return the CN
                return GetCNFromDN(distinguishedName);
            }
            return GetCNFromDN(distinguishedName);
        }

        static string GetCNFromDN(string distinguishedName)
        {
            // Extract CN from DN (e.g., "CN=John Doe,OU=Users,DC=Fahr,DC=gov,DC=ae" -> "John Doe")
            if (distinguishedName.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                int endIndex = distinguishedName.IndexOf(',');
                if (endIndex > 0)
                {
                    return distinguishedName.Substring(3, endIndex - 3);
                }
            }
            return distinguishedName;
        }

        static string EscapeLdapFilter(string value)
        {
            // Escape special LDAP filter characters
            if (string.IsNullOrEmpty(value)) return value;

            return value
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00");
        }
    }
}