using LiberationFleet.Server.Application.Common.Interfaces;
using BC = BCrypt.Net.BCrypt;

namespace LiberationFleet.Server.Infrastructure.Security;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BC.HashPassword(password);

    public bool Verify(string password, string passwordHash) => BC.Verify(password, passwordHash);
}
