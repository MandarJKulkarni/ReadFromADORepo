using Microsoft.Extensions.Caching.Memory;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ADOService
{
    public class ADOService : IADOService
    {
        private readonly IGitHttpClient _gitHttpClient;
        private readonly IMemoryCache _memoryCache;
        public string ProjectName { get; set; }
        public string RepoName { get; set; }

        public ADOService(IMemoryCache memoryCache, IConfiguration configuration)
        {
            string collectionUri = configuration["ADO:CollectionUri"]; //."https://dev.azure.com/myADOorg";

            ProjectName = configuration["ADO:ProjectName"]; //"myProject";
            RepoName = configuration["ADO:RepoName"];// "MyRepositoryInADO";
            const string pat = "XXXX";// Read it from some key vault service

            VssBasicCredential creds = new VssBasicCredential(string.Empty, pat);
            // Connect to Azure DevOps Service
            VssConnection vssConnection = new VssConnection(new Uri(collectionUri), creds);
            GitHttpClient gitHttpClient = vssConnection.GetClient<GitHttpClient>();
            _gitHttpClient = new GitHttpClientAdapter(gitHttpClient);
            _memoryCache = memoryCache;
        }

        public ADOService(IGitHttpClient gitHttpClient, IMemoryCache memoryCache)
        {
            this._gitHttpClient = gitHttpClient ?? throw new ArgumentNullException(nameof(gitHttpClient));
            this._memoryCache = memoryCache;
        }

        public async Task<List<string>> GetFolderNamesInADORepo(string folderPath)
        {
            if(string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentNullException(nameof(folderPath));
            }

            var cacheKeyFolderNames = "folderNames" + $"{ProjectName}:{RepoName}:{folderPath}";
            if (_memoryCache.TryGetValue(cacheKeyFolderNames, out List<string> folderNames))
                return folderNames;

            var repo = await _gitHttpClient.GetRepositoryAsync(ProjectName, RepoName);

            if (repo == null)
            {
                throw new InvalidOperationException($"Repository '{RepoName}' not found in project '{ProjectName}'.");
            }

            List<GitItem> items = await _gitHttpClient.GetItemsAsync(ProjectName,
            repo.Id,
            scopePath: folderPath,
            recursionLevel: VersionControlRecursionType.OneLevel);

            List<string> foldernames = new List<string>();
            foreach (GitItem item in items)
            {
                string itemPath = item.Path;
                if (item.IsFolder && itemPath != folderPath)
                    foldernames.Add(itemPath.TrimStart('/'));
            }
            _memoryCache.Set(cacheKeyFolderNames, foldernames, DateTimeOffset.Now.AddMinutes(30));
            return foldernames;

        }

        public async Task<List<string>> GetFileNamesInADORepo(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentNullException(nameof(folderPath));
            }
            var cacheKeyFileNames = "fileNames" + $"{ProjectName}:{RepoName}:{folderPath}";
            if (_memoryCache.TryGetValue(cacheKeyFileNames, out List<string> fileNames))
                return fileNames;

            var repo = await _gitHttpClient.GetRepositoryAsync(ProjectName, RepoName);
            if (repo == null)
            {
                throw new InvalidOperationException($"Repository '{RepoName}' not found in project '{ProjectName}'.");
            }

            List<GitItem> items = await _gitHttpClient.GetItemsAsync(ProjectName, repo.Id, scopePath: folderPath, recursionLevel: VersionControlRecursionType.OneLevel);
            List<string> fileNamesFromRepo = new List<string>();
            foreach (GitItem item in items)
            {
                if (!item.IsFolder)
                {
                    string itemPath = item.Path;
                    fileNamesFromRepo.Add(itemPath.Substring(folderPath.Length + 1));
                }
            }
            _memoryCache.Set(cacheKeyFileNames, fileNamesFromRepo, DateTimeOffset.Now.AddMinutes(30));
            return fileNamesFromRepo;

        }

        public async Task<JObject?> GetFileContentFromADORepo(string folderPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentNullException(nameof(folderPath));
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }
             var cacheKeyFileContent = "fileContent" + $"{ProjectName} : {RepoName} :{folderPath}:{fileName}";
            if (_memoryCache.TryGetValue(cacheKeyFileContent, out JObject fileContent))
                return fileContent;

            var repo = await _gitHttpClient.GetRepositoryAsync(ProjectName, RepoName);
            if (repo == null)
            {
                throw new InvalidOperationException($"Repository '{RepoName}' not found in project '{ProjectName}'.");
            }

            List<GitItem> items = await _gitHttpClient.GetItemsAsync(ProjectName, repo.Id, scopePath: folderPath, recursionLevel: VersionControlRecursionType.OneLevel);
            foreach (GitItem item in items)
            {
                if (item.IsFolder)
                    continue;   // Do nothing for now for subfolders. 
                string itemPath = item.Path;
                string filenameInFolder = itemPath.Substring(folderPath.Length + 1);
                if (filenameInFolder == fileName)
                {
                    Stream contentStream = await _gitHttpClient.GetItemContentAsync(repo.Id, itemPath);
                    using (StreamReader reader = new StreamReader(contentStream))
                    {
                        string fileContentFromReader = reader.ReadToEnd();
                        var fileContentJObject = JObject.Parse(fileContentFromReader);
                        _memoryCache.Set(cacheKeyFileContent, fileContentJObject, DateTimeOffset.Now.AddMinutes(30));
                        return fileContentJObject;
                    }
                }
            }
            return null;

        }

    }
}
