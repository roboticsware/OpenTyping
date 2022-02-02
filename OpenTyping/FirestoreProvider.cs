using Google.Cloud.Firestore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenTyping
{
    public class FirestoreProvider
    {
        private FirestoreDb _fireStoreDb = null;

        public FirestoreProvider() { }

        public bool OpenDatabase()
        {
            _fireStoreDb = new FirestoreDbBuilder
            {
                ProjectId = "roboticsware-uz",
                JsonCredentials = Properties.Resources.private_key_for_firestore
            }.Build();

            return (_fireStoreDb != null) ?  true : false;
        }

        public async Task<IReadOnlyCollection<T>> GetUsersAsync<T>()
        {
            var collection = _fireStoreDb.Collection("Users");
            var snapshot = await collection.GetSnapshotAsync();
            return snapshot.Documents.Select(x => x.ConvertTo<T>()).ToList();
        }

        public async Task<WriteResult> AddAsync<T>(T entity)
        {
            var document = _fireStoreDb.Collection("Users").Document();
            var result = await document.CreateAsync(entity);
            return result;
        }
    }
}
