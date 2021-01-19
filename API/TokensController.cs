using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QnABot.DAL;

namespace QnABot.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokensController : ControllerBase
    {
        private readonly TokensContext db;
        public TokensController(TokensContext _db)
        {

            db = _db;
        }
        /// <summary>
        /// Gel All Tokens
        /// </summary>
        /// <returns></returns>
        // GET: api/Tokens
        [HttpGet]
        public IEnumerable<Token> Get()
        {
            return db.Tokens.ToList();
        }
        /// <summary>
        /// Get Token By guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        // GET: api/Tokens/5
        [HttpGet("{guid}", Name = "GetTokens")]
        public async Task<Token>  Get(string guid)
        {
            if (!ModelState.IsValid)
            {
                return null;
            }
            var token = await db.Tokens.SingleAsync(s => s.GUID == new Guid(guid));

            if (token == null)
            {
                return null;
            }

            return token;
        }
        /// <summary>
        /// Create a Token
        /// </summary>
        /// <param name="tokenId"></param>
        // POST: api/Tokens
        [HttpPost]
        public Guid Post(string tenantId, string url, string groupId, string mail)
        {
           var GUID= Guid.NewGuid();
          var token= new Token { GUID = GUID, TenantId= tenantId, Url= url, GroupId =groupId, Mail= mail };
            db.Add(token);
            try
            {
                db.SaveChanges();
                return GUID;
            }
            catch (Exception ex)
            {
                throw new Exception("Error in save data to database"+ ex.Message);
            }
           
        }
        /// <summary>
        /// Update a token
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        // PUT: api/Tokens/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
