using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServarrAPI.Model
{
    public interface IUpdateService
    {
        Task<IEnumerable<UpdateEntity>> All();
        Task<UpdateEntity> Insert(UpdateEntity entity);
        Task<UpdateEntity> Find(string version, string branch);
    }

    public class UpdateService : IUpdateService
    {
        private readonly IUpdateRepository _repo;

        public UpdateService(IUpdateRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<UpdateEntity>> All()
        {
            return _repo.All();
        }

        public Task<UpdateEntity> Insert(UpdateEntity entity)
        {
            return _repo.Insert(entity);
        }

        public Task<UpdateEntity> Find(string version, string branch)
        {
            return _repo.Find(version, branch);
        }
    }
}
