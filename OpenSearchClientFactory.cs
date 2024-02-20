using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using ATS.Common.Poco;
using Microsoft.Extensions.Configuration;
using OpenSearch.Client;
using OpenSearch.Net.Auth.AwsSigV4;
using Serilog;

namespace ATS.DarkSearch;

public class OpenSearchClientFactory
{
    private readonly IConfiguration _config;
    private AssumeRoleAWSCredentials _credentials;

    public OpenSearchClientFactory(IConfiguration config)
    {
        _config = config;

        var connectionString = _config["ConnectionStrings:Elastic"];
        Log.Information($"Configuring Elastic with {connectionString}");

        var client = Create();

        if (!client.Indices.Exists(Indices.Parse(PingsRepository.PingsIndex)).Exists)
        {
            var response = client.Indices.Create(Indices.Index(PingsRepository.PingsIndex),
                index => index.Map<PingResultPoco>(
                    x => x.AutoMap()
                ));
            if (response.ServerError != null)
                Log.Error("Failed to request Elastic with error: " + response.ServerError.Error);
        }
    }

    public OpenSearchClient Create()
    {
        if (_credentials == null)
            _credentials = GetCredentialsAsync().GetAwaiter().GetResult();
        else
            _credentials.GetCredentials();
        
        var connectionString = _config["ConnectionStrings:Elastic"];

        var endpoint = new Uri(connectionString);

        var connection = new AwsSigV4HttpConnection(_credentials, RegionEndpoint.EUWest2);
        //var connection = new AwsSigV4HttpConnection(RegionEndpoint.EUWest2);

        var config = new ConnectionSettings(endpoint, connection)
            .DefaultIndex(PingsRepository.PingsIndex);

        return new OpenSearchClient(config);
    }

    private async Task<AssumeRoleAWSCredentials> GetCredentialsAsync()
    {
        var roleSessionName = "ats-darksearch-ossession";
        var stsClient = new AmazonSecurityTokenServiceClient(RegionEndpoint.EUWest2);
        var assumeRequest = new AssumeRoleRequest
        {
            RoleArn = _config["AppSettings:RoleArn"],
            RoleSessionName = roleSessionName
        };

        var assumeResponse = await stsClient.AssumeRoleAsync(assumeRequest);

        return new AssumeRoleAWSCredentials(
            assumeResponse.Credentials,
            _config["AppSettings:RoleArn"],
            roleSessionName
        );
    }
}