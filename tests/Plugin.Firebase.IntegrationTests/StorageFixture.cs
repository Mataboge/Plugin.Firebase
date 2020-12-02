using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.Firebase.Storage;
using Xamarin.Essentials;
using Xunit;

namespace Plugin.Firebase.IntegrationTests
{
    public sealed class StorageFixture : IDisposable
    {
        [Fact]
        public void gets_root_reference()
        {
            var reference = CrossFirebaseStorage.Current.GetRootReference();
            
            Assert.NotNull(reference);
            Assert.Null(reference.Parent);
            Assert.Equal("/", reference.FullPath);
            Assert.Equal("", reference.Name);
            Assert.Equal("pluginfirebase-integrationtest.appspot.com", reference.Bucket);
        }
        
        [Fact]
        public void gets_reference_from_url()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromUrl("gs://pluginfirebase-integrationtest.appspot.com/files_to_keep/text_1.txt");
            
            Assert.NotNull(reference.Root);
            Assert.NotNull(reference.Parent);
            Assert.Equal("/files_to_keep/text_1.txt", reference.FullPath);
            Assert.Equal("text_1.txt", reference.Name);
            Assert.Equal("pluginfirebase-integrationtest.appspot.com", reference.Bucket);
        }
        
        [Fact]
        public void gets_reference_from_path()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath("files_to_keep/text_1.txt");
            
            Assert.NotNull(reference.Root);
            Assert.NotNull(reference.Parent);
            Assert.Equal("/files_to_keep/text_1.txt", reference.FullPath);
            Assert.Equal("text_1.txt", reference.Name);
            Assert.Equal("pluginfirebase-integrationtest.appspot.com", reference.Bucket);
        }
        
        [Fact]
        public void gets_child_reference()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetRootReference().GetChild("files_to_keep/text_1.txt");
            
            Assert.NotNull(reference.Root);
            Assert.NotNull(reference.Parent);
            Assert.Equal("/files_to_keep/text_1.txt", reference.FullPath);
            Assert.Equal("text_1.txt", reference.Name);
            Assert.Equal("pluginfirebase-integrationtest.appspot.com", reference.Bucket);
        }

        [Fact]
        public async Task gets_download_url()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath("files_to_keep/text_1.txt");

            var downloadUrl = await reference.GetDownloadUrlAsync();
            Assert.StartsWith(CreateDownloadUrl("files_to_keep/text_1.txt"), downloadUrl);
        }

        private static string CreateDownloadUrl(string pathToFile)
        {
            return $"https://firebasestorage.googleapis.com/v0/b/pluginfirebase-integrationtest.appspot.com/o/{pathToFile}?alt=media&token=";
        }

        [Fact]
        public async Task uploads_via_byte_array()
        {
            var path = $"texts/via_bytes.txt";
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath(path);

            await reference.PutBytes(Encoding.UTF8.GetBytes("Some test text")).AwaitAsync();
            var downloadUrl = await reference.GetDownloadUrlAsync();
            Assert.StartsWith(CreateDownloadUrl(path), downloadUrl);
        }

        [Fact]
        public async Task uploads_via_stream()
        {
            var path = $"texts/via_stream.txt";
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath(path);

            using(var stream = await CreateTextStreamAsync("Some text via stream")) {
                await reference.PutStream(stream).AwaitAsync();
                var downloadUrl = await reference.GetDownloadUrlAsync();
                Assert.StartsWith(CreateDownloadUrl(path), downloadUrl);
            }
        }

        private static async Task<Stream> CreateTextStreamAsync(string text)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            await writer.WriteAsync(text);
            await writer.FlushAsync();
            return stream;
        }

        [Fact]
        public async Task uploads_stream_with_meta_data()
        {
            var path = $"texts/via_stream_with_metadata.txt";
            var metadata = new StorageMetadata(contentType:"text/plain");
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath(path);

            await reference.PutBytes(Encoding.UTF8.GetBytes("Some test text"), metadata).AwaitAsync();
            var uploadedMetadata = await reference.GetMetadataAsync();
            
            Assert.Equal(path, uploadedMetadata.Path);
            Assert.Equal("text/plain", uploadedMetadata.ContentType);
            Assert.Equal(14, uploadedMetadata.Size);

            var customData = new Dictionary<string, string> { { "some_key", "some_value" } };
            var updatedMetadata = await reference.UpdateMetadataAsync(new StorageMetadata(contentType:"text/html", customMetadata:customData));
            
            Assert.Equal(path, updatedMetadata.Path);
            Assert.Equal("text/html", updatedMetadata.ContentType);
            Assert.Equal(customData, updatedMetadata.CustomMetadata);
        }

        [Fact]
        public async Task lists_files_with_limit()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath("files_to_keep");

            var result = await reference.ListAsync(2);
            Assert.Equal(2, result.Items.Count());
        }

        [Fact]
        public async Task lists_all_files()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath("files_to_keep");

            var result = await reference.ListAllAsync();
            Assert.Equal(3, result.Items.Count());
        }

        [Fact]
        public async Task gets_data_as_stream()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath("files_to_keep/text_1.txt");

            var stream = await reference.GetStreamAsync(1 * 1024 * 1024);
            Assert.NotNull(stream);
        }

        [Fact]
        public async Task downloads_file()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath("files_to_keep/text_1.txt");

            Assert.True(await reference.DownloadFileAsync($"{FileSystem.CacheDirectory}/test.txt"));
        }

        [Fact]
        public async Task deletes_file()
        {
            var reference = CrossFirebaseStorage
                .Current
                .GetReferenceFromPath("files_to_delete");
            
            Assert.Empty((await reference.ListAllAsync()).Items);
            await reference.GetChild("text.txt").PutBytes(Encoding.UTF8.GetBytes("This file should get deleted")).AwaitAsync();
            Assert.Single((await reference.ListAllAsync()).Items);

            await reference.GetChild("text.txt").DeleteAsync();
            Assert.Empty((await reference.ListAllAsync()).Items);
        }

        public async void Dispose()
        {
            var rootReference = CrossFirebaseStorage.Current.GetRootReference();
            var filesToDelete = (await rootReference.GetChild("files_to_delete").ListAllAsync()).Items;
            var texts = (await rootReference.GetChild("texts").ListAllAsync()).Items;
            await Task.WhenAll(filesToDelete.Select(TryDeleteAsync).Concat(texts.Select(TryDeleteAsync)));
        }

        private static async Task TryDeleteAsync(IStorageReference reference)
        {
            try {
                await reference.DeleteAsync();
            } catch(Exception e) {
                Console.WriteLine(e);
            }
        }
    }
}