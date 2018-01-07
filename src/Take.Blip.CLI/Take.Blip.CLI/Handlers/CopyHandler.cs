﻿using ITGlobal.CommandLine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Take.BlipCLI.Services.Interfaces;
using Take.BlipCLI.Services;
using Lime.Protocol;
using Take.BlipCLI.Services.Settings;

namespace Take.BlipCLI.Handlers
{
    public class CopyHandler : HandlerAsync
    {
        public INamedParameter<string> From { get; set; }
        public INamedParameter<string> FromAuthorization { get; set; }
        public INamedParameter<string> To { get; set; }
        public INamedParameter<string> ToAuthorization { get; set; }
        public INamedParameter<List<BucketNamespace>> Contents { get; set; }

        private readonly ISettingsFile _settingsFile;

        public CopyHandler()
        {
            _settingsFile = new SettingsFile();
        }

        public async override Task<int> RunAsync(string[] args)
        {
            if ((!From.IsSet && !FromAuthorization.IsSet) || (!To.IsSet && !ToAuthorization.IsSet))
                throw new ArgumentNullException("You must provide from and to parameters for this action. Use '-f' [--from] (or '-fa' [--fromAuthorization]) and '-t' [--to] (or '-ta' [--toAuthorization]) parameters");

            string fromAuthorization = FromAuthorization.Value;
            string toAuthorization = ToAuthorization.Value;

            if (From.IsSet)
            {
                fromAuthorization = _settingsFile.GetNodeCredentials(Node.Parse(From.Value)).Authorization;
            }

            if (To.IsSet)
            {
                toAuthorization = _settingsFile.GetNodeCredentials(Node.Parse(To.Value)).Authorization;
            }

            IBlipBucketClient sourceBlipBucketClient = new BlipHttpClientAsync(fromAuthorization);
            IBlipBucketClient targetBlipBucketClient = new BlipHttpClientAsync(toAuthorization);

            foreach (var content in Contents.Value)
            {
                var documentKeysToCopy = await sourceBlipBucketClient.GetAllDocumentKeysAsync(content) ?? new DocumentCollection();
                var documentPairsToCopy = await sourceBlipBucketClient.GetAllDocumentsAsync(documentKeysToCopy, content);

                if (documentPairsToCopy != null)
                {
                    foreach (var resourcePair in documentPairsToCopy)
                    {
                        await targetBlipBucketClient.AddDocumentAsync(resourcePair.Key, resourcePair.Value, content);
                    }
                }
            }

            return 0;
        }

        public List<BucketNamespace> CustomParser(string contents)
        {
            var defaultContents = new List<BucketNamespace> {
                BucketNamespace.AIModel,
                BucketNamespace.Document,
                BucketNamespace.Profile,
                BucketNamespace.Resource
            };

            if (string.IsNullOrWhiteSpace(contents)) return defaultContents;

            var contentsList = new List<BucketNamespace>();
            var contentsArray = contents.Split(',');

            foreach (var content in contentsArray)
            {
                var contentType = TryGetContentType(content);
                if (contentType.HasValue)
                {
                    contentsList.Add(contentType.Value);
                }
            }

            if(contentsList.Count == 0) return defaultContents;

            return contentsList;
        }

        private BucketNamespace? TryGetContentType(string content)
        {
            var validContents = Enum.GetNames(typeof(BucketNamespace));
            var validContent = validContents.FirstOrDefault(c => c.ToLowerInvariant().Equals(content.ToLowerInvariant()));

            if (validContent != null)
                return Enum.Parse<BucketNamespace>(validContent);

            return null;
        }
    }

    public enum BucketNamespace
    {
        Resource,
        Document,
        Profile,
        AIModel
    }
}