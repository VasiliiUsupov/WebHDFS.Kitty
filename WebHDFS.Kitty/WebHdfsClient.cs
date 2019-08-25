﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebHDFS.Kitty.DataModels;

namespace WebHDFS.Kitty
{
    public class WebHdfsClient : IWebHdfsClient
    {
        private readonly HttpClient _httpClient;

        public WebHdfsClient(string baseAddress = null, HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient (new HttpClientHandler { UseDefaultCredentials = true, AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(baseAddress ?? "")
            };
        }

        public async Task<bool> MakeDirectory(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/webhdfs/v1/{path.TrimStart('/')}?op=MKDIRS&permission=770");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Not success status code. Code={response.StatusCode}. Content={content}");
            }
            //TODO: check boolean response!
            return true;
        }

        public async Task UploadFile(string path, Stream fileStream)
        {
            var initRequest = new HttpRequestMessage(HttpMethod.Put, $"/webhdfs/v1/{path.TrimStart('/')}?op=CREATE&noredirect=false&overwrite=true&permission=770");
            initRequest.Content = new StreamContent(Stream.Null);
            initRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var initResponse = await _httpClient.SendAsync(initRequest);
            //var headers = initResponse.Headers;
            //var content = initResponse.Content.ReadAsStringAsync().Result;
            if (initResponse.StatusCode == HttpStatusCode.TemporaryRedirect)
            {
                var uploadRequest = new HttpRequestMessage(HttpMethod.Put, initResponse.Headers.Location);
                uploadRequest.Content = new StreamContent(fileStream);
                uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var uploadResponse = await _httpClient.SendAsync(uploadRequest);
                uploadResponse.EnsureSuccessStatusCode();
            }
        }

        public async Task<IReadOnlyCollection<FileStatus>> ListDirectory(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/webhdfs/v1/{path.TrimStart('/')}?op=LISTSTATUS");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var notSuccessResponseContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Not success status code. Code={response.StatusCode}. Content={notSuccessResponseContent}");
            }
            var content = await response.Content.ReadAsStringAsync();
            var deserializedContent= JsonConvert.DeserializeObject<ListDirResponse>(content);
            return deserializedContent.FileStatusCollection.FileStatuses;
        }

        public async Task<Stream> ReadFileStream(string path)
        {
            var initRequest = new HttpRequestMessage(HttpMethod.Get, $"/webhdfs/v1/{path.TrimStart('/')}?op=OPEN");
            var initResponse = await _httpClient.SendAsync(initRequest);

            if (initResponse.StatusCode == HttpStatusCode.TemporaryRedirect)
            {
                var downloadRequest = new HttpRequestMessage(HttpMethod.Get, initResponse.Headers.Location);
                var downloadResponse = await _httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    var notSuccessDownloadResponseContent = await downloadResponse.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Not success status code. Code={downloadResponse.StatusCode}. Content={notSuccessDownloadResponseContent}");
                }

                return await downloadResponse.Content.ReadAsStreamAsync();
            }
            else if (!initResponse.IsSuccessStatusCode)
            {
                var notSuccessInitResponseContent = await initResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Not success status code. Code={initResponse.StatusCode}. Content={notSuccessInitResponseContent}");
            }
            else
            {
                return await initResponse.Content.ReadAsStreamAsync();
            }
        }
    }
}