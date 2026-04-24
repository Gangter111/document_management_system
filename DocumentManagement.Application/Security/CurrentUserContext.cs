using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Security;

public sealed class CurrentUserContext : ICurrentUserContext
{
    public UserSession? User { get; private set; }

    public bool IsAuthenticated => User != null;

    public void Set(UserSession user)
    {
        User = user;
    }

    public void Clear()
    {
        User = null;
    }
}