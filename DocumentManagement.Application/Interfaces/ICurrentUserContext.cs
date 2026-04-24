using DocumentManagement.Application.Models;

namespace DocumentManagement.Application.Interfaces;

public interface ICurrentUserContext
{
    UserSession? User { get; }
    bool IsAuthenticated { get; }

    void Set(UserSession user);
    void Clear();
}