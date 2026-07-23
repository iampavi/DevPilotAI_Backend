using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Shared.Common;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace DevPilotAI.Infrastructure.Services;

public class GitRepositoryService : IGitRepositoryService
{
    public Task<Result> CloneRepositoryAsync(
        string repositoryUrl,
        string branch,
        string? personalAccessToken,
        string destinationPath,
        Action<int> onProgress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }

            var options = new CloneOptions
            {
                BranchName = branch,
                Checkout = true
            };

            options.FetchOptions.OnTransferProgress = progress =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false; // Cancels clone
                }

                if (progress.TotalObjects > 0)
                {
                    var percentage = (int)((double)progress.ReceivedObjects / progress.TotalObjects * 100);
                    onProgress(Math.Clamp(percentage, 0, 100));
                }
                return true;
            };

            // Set credentials if PAT is provided
            if (!string.IsNullOrEmpty(personalAccessToken))
            {
                options.FetchOptions.CredentialsProvider = (url, userSpec, cred) => new UsernamePasswordCredentials
                {
                    Username = personalAccessToken,
                    Password = "" // standard token-based authentication format for GitHub/GitLab
                };
            }

            Repository.Clone(repositoryUrl, destinationPath, options);

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure(new Error("Git.CloneError", ex.Message)));
        }
    }
}
