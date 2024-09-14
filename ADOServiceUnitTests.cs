using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Caching.Memory;

namespace ADOServiceUnitTests
{
    [TestClass()]
    public class ADOServiceUnitTests
    {
        private List<GitItem> gitItems;
        private MemoryCache memoryCache;
        private Mock<IGitHttpClient> mockGitHttpClient;
        public ADOServiceUnitTests()
        {
            GitItem rootFolder = new GitItem("/folder1", "111", GitObjectType.Blob, "111", 1);
            rootFolder.IsFolder = true;
            GitItem subFolder = new GitItem("/folder2", "222", GitObjectType.Blob, "222", 2);
            subFolder.IsFolder = true;
            GitItem file2Item = new GitItem("/folder2/file2.json", "33", GitObjectType.Blob, "333", 3);
            file2Item.IsFolder = false;
            GitItem file1Item = new GitItem("/folder2/file1.json", "44", GitObjectType.Blob, "444", 4);
            file1Item.IsFolder = false;

            gitItems = new List<GitItem>();
            gitItems.Add(rootFolder);
            gitItems.Add(subFolder);
            gitItems.Add(file2Item);
            gitItems.Add(file1Item);

            memoryCache = new MemoryCache(new MemoryCacheOptions());
            mockGitHttpClient = new Mock<IGitHttpClient>();
        }

        [TestMethod()]
        public void ADOServiceTest()
        {
            var mockGitHttpClient = new Mock<IGitHttpClient>();
            var mockMemoryCache = new MemoryCache(new MemoryCacheOptions());
            ADOService _ADOService = new ADOService(mockGitHttpClient.Object, mockMemoryCache);
            Assert.IsNotNull(_ADOService);
        }

        [TestMethod()]
        public void GetFolderNamesInAzureDevopsRepoTest()
        {
            mockGitHttpClient.Setup(x => x.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>())).
                                        ReturnsAsync(new GitRepository());
            mockGitHttpClient.Setup(x => x.GetItemsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<VersionControlRecursionType>())).
                                        ReturnsAsync(gitItems);
            var folderNamesCache = new List<string>();

            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            ads.RepoName = "testrepo";
            ads.ProjectName = "testproject";
            var foldernames = ads.GetFolderNamesInAzureDevopsRepo("/").Result;
            Assert.IsFalse(foldernames.IsNullOrEmpty());
            Assert.AreEqual(2, foldernames.Count);
            Assert.IsTrue(foldernames.Contains("folder1") && foldernames.Contains("folder2"));

            var folderNamesFromCache = ads.GetFolderNamesInAzureDevopsRepo("/").Result;
            Assert.IsFalse(folderNamesFromCache.IsNullOrEmpty());
            Assert.AreEqual(2, folderNamesFromCache.Count);
            Assert.IsTrue(folderNamesFromCache.Contains("folder1") && foldernames.Contains("folder2"));

        }

        [TestMethod()]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFolderNamesInAzureDevopsRepoTest_NullArguments()
        {
            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            var foldernames = await ads.GetFolderNamesInAzureDevopsRepo("");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GetFolderNamesInAzureDevopsRepoTest_NullRepository()
        {
            mockGitHttpClient.Setup(c => c.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((GitRepository)null);
            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            ads.RepoName = "testrepo";
            ads.ProjectName = "testproject";
            var foldernames = await ads.GetFolderNamesInAzureDevopsRepo("/");
        }

        [TestMethod()]
        public async Task GetFileNamesInAzureDevopsRepoTest()
        {
            mockGitHttpClient.Setup(x => x.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>())).
                                        ReturnsAsync(new GitRepository());
            mockGitHttpClient.Setup(x => x.GetItemsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<VersionControlRecursionType>())).
                                        ReturnsAsync(gitItems);
            
            ADOService ads = new ADOService(mockGitHttpClient.Object,memoryCache);
            ads.RepoName = "testrepo";
            ads.ProjectName = "testproject";
            var fileNames = await ads.GetFileNamesInAzureDevopsRepo("/folder2");
            Assert.IsFalse(fileNames.IsNullOrEmpty());
            Assert.AreEqual(2, fileNames.Count);
            Assert.IsTrue(fileNames.Contains("file2.json") && fileNames.Contains("file1.json"));

            var fileNamesFromCache = await ads.GetFileNamesInAzureDevopsRepo("/folder2");
            Assert.IsFalse(fileNamesFromCache.IsNullOrEmpty());
            Assert.AreEqual(2, fileNamesFromCache.Count);
            Assert.IsTrue(fileNamesFromCache.Contains("file2.json") && fileNamesFromCache.Contains("file1.json"));
        }
        [TestMethod()]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFileNamesInAzureDevopsRepoTest_NullArguments()
        {
            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            var foldernames = await ads.GetFileNamesInAzureDevopsRepo("");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GetFileNamesInAzureDevopsRepoTest_NullRepository()
        {
            mockGitHttpClient.Setup(c => c.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((GitRepository)null);
            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            ads.RepoName = "testrepo";
            ads.ProjectName = "testproject";
            var foldernames = await ads.GetFileNamesInAzureDevopsRepo("/folder2");
        }

        [TestMethod()]
        public async Task GetFileContentFromAzureDevopsRepoTest()
        {

            string mockContent = "{\r\n \"property1\":"\"value1\",\"property2\":"\"value2\"}";
            JObject mockContentObject = JObject.Parse(mockContent);
            Stream sc = new MemoryStream(Encoding.UTF8.GetBytes(mockContent));
            mockGitHttpClient.Setup(x => x.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>())).
                                        ReturnsAsync(new GitRepository());
            mockGitHttpClient.Setup(x => x.GetItemsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<VersionControlRecursionType>())).
                                        ReturnsAsync(gitItems);
            mockGitHttpClient.Setup(x => x.GetItemContentAsync(It.IsAny<Guid>(), It.IsAny<string>())).
                                        ReturnsAsync(sc);

            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            ads.RepoName = "testrepo";
            ads.ProjectName = "testproject";
            JObject fileContent = await ads.GetFileContentFromAzureDevopsRepo("/folder2", "file2.json");
            Assert.IsNotNull(fileContent);
            Assert.IsTrue(JToken.DeepEquals(mockContentObject, fileContent));

            JObject fileContentFromCache = await ads.GetFileContentFromAzureDevopsRepo("/folder2", "file2.json");
            Assert.IsNotNull(fileContentFromCache);
            Assert.IsTrue(JToken.DeepEquals(mockContentObject, fileContentFromCache));

        }

        [TestMethod]
        public async Task GetFileContentFromAzureDevopsRepoTest_NullContent()
        {
            string mockContent = "{\r\n \"property1\":"\"value1\",\"property2\":"\"value2\"}";
            JObject mockContentObject = JObject.Parse(mockContent);
            Stream sc = new MemoryStream(Encoding.UTF8.GetBytes(mockContent));
            mockGitHttpClient.Setup(x => x.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>())).
                                        ReturnsAsync(new GitRepository());
            mockGitHttpClient.Setup(x => x.GetItemsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<VersionControlRecursionType>())).
                                        ReturnsAsync(gitItems);
            mockGitHttpClient.Setup(x => x.GetItemContentAsync(It.IsAny<Guid>(), It.IsAny<string>())).
                                        ReturnsAsync(sc);

            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            ads.RepoName = "testrepo";
            ads.ProjectName = "testproject";
            JObject fileContent = await ads.GetFileContentFromAzureDevopsRepo("/folder2", "nonexistentfilename");
            Assert.IsNull(fileContent);
        }

        [TestMethod()]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFileContentFromAzureDevopsRepoTest_NullFolderPath()
        {
            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            var fileContent = await ads.GetFileContentFromAzureDevopsRepo(" ", "file1.json");
        }

        [TestMethod()]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFileContentFromAzureDevopsRepoTest_NullScope()
        {
            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            var fileContent = await ads.GetFileContentFromAzureDevopsRepo("/folder2", " ");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GetFileContentFromAzureDevopsRepoTest_NullRepository()
        {
            mockGitHttpClient.Setup(c => c.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((GitRepository)null);
            ADOService ads = new ADOService(mockGitHttpClient.Object, memoryCache);
            ads.RepoName = "testrepo";
            ads.ProjectName = "testproject";
            var fileContent = await ads.GetFileContentFromAzureDevopsRepo("/folder2", "file1.json");
        }

    }
}