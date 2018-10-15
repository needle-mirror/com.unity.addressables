using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using NUnit.Framework;

namespace UnityEditor.AddressableAssets.Tests
{
    public class HttpHostingServiceTests
    {
        private class MyWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                var w = base.GetWebRequest(uri);
                Debug.Assert(w != null);
                w.Timeout = 2000;
                return w;
            }
        }

        private HttpHostingService m_service;
        private string m_contentRoot;
        private readonly WebClient m_client;

        public HttpHostingServiceTests()
        {
            m_client = new MyWebClient();
        }

        private static byte[] GetRandomBytes(int size)
        {
            var rand = new Random();
            var buf = new byte[size];
            rand.NextBytes(buf);
            return buf;
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            m_service = new HttpHostingService();
            var dirName = Path.GetRandomFileName();
            m_contentRoot = Path.Combine(Path.GetTempPath(), dirName);
            Assert.IsNotEmpty(m_contentRoot);
            Directory.CreateDirectory(m_contentRoot);
            m_service.HostingServiceContentRoots.Add(m_contentRoot);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            m_service.StopHostingService();

            if (!string.IsNullOrEmpty(m_contentRoot) && Directory.Exists(m_contentRoot))
                Directory.Delete(m_contentRoot, true);
        }

        [Test]
        public void ShouldServeRequestedFiles()
        {
            var fileNames = new[]
            {
                Path.GetRandomFileName(),
                Path.Combine("subdir", Path.GetRandomFileName()),
                Path.Combine("subdir1", Path.Combine("subdir2", Path.GetRandomFileName()))
            };

            foreach (var fileName in fileNames)
            {
                var filePath = Path.Combine(m_contentRoot, fileName);
                var bytes = GetRandomBytes(1024);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllBytes(filePath, bytes);
                m_service.StartHostingService();
                Assert.IsTrue(m_service.IsHostingServiceRunning);
                var url = string.Format("http://127.0.0.1:{0}/{1}", m_service.HostingServicePort, fileName);
                try
                {
                    var data = m_client.DownloadData(url);
                    Assert.AreEqual(data.Length, bytes.Length);
                    for (var i = 0; i < data.Length; i++)
                        if (bytes[i] != data[i])
                            Assert.Fail("Data does not match {0} != {1}", bytes[i], data[i]);
                }
                catch (Exception e)
                {
                    Assert.Fail(e.Message);
                }
            }
        }

        [Test]
        public void ShouldRespondWithStatus404IfFileDoesNotExist()
        {
            m_service.StartHostingService();
            Assert.IsTrue(m_service.IsHostingServiceRunning);
            var url = string.Format("http://127.0.0.1:{0}/{1}", m_service.HostingServicePort, "foo");
            try
            {
                m_client.DownloadData(url);
            }
            catch (WebException e)
            {
                var response = (HttpWebResponse) e.Response;
                Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                Assert.Fail();
            }
        }

        // StartHostingService

        [Test]
        public void StartHostingServiceShould_AssignPortIfUnassigned()
        {
            m_service.StartHostingService();
            Assert.Greater(m_service.HostingServicePort, 0);
        }

        // OnBeforeSerialize

        [Test]
        public void OnBeforeSerializeShould_PersistExpectedDataToKeyDataStore()
        {
            m_service.StartHostingService();
            var port = m_service.HostingServicePort;
            var data = new KeyDataStore();
            m_service.OnBeforeSerialize(data);
            Assert.AreEqual(port, data.GetData("HostingServicePort", 0));
        }

        // OnAfterDeserialize

        [Test]
        public void OnAfterDeserializeShould_RestoreExpectedDataFromKeyDataStore()
        {
            var data = new KeyDataStore();
            data.SetData("HostingServicePort", 1234);
            m_service.OnAfterDeserialize(data);
            Assert.AreEqual(1234, m_service.HostingServicePort);
        }

        // ResetListenPort

        [Test]
        public void ResetListenPortShould_AssignTheGivenPort()
        {
            m_service.ResetListenPort(1234);
            Assert.AreEqual(1234, m_service.HostingServicePort);
        }

        [Test]
        public void ResetListenPortShould_AssignRandomPortIfZero()
        {
            m_service.ResetListenPort();
            m_service.StartHostingService();
            Assert.Greater(m_service.HostingServicePort, 0);
        }

        [Test]
        public void ResetListenPortShouldNot_StartServiceIfItIsNotRunning()
        {
            m_service.StopHostingService();
            m_service.ResetListenPort();
            Assert.IsFalse(m_service.IsHostingServiceRunning);
        }

        [Test]
        public void ResetListenPortShould_RestartServiceIfRunning()
        {
            m_service.StartHostingService();
            m_service.ResetListenPort();
            Assert.IsTrue(m_service.IsHostingServiceRunning);
        }
    }
}