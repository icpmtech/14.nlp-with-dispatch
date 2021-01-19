using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnABot.DAL
{
    public static class DbInitializer
    {
        public static void Initialize(TokensContext context)
        {
            context.Database.EnsureCreated();

            // Look for any students.
            if (context.Tokens.Any())
            {
                return;   // DB has been seeded
            }
            
        }
    }
}
