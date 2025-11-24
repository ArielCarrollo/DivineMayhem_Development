using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using System;
using System.Threading.Tasks;

public class LeaderBoards : MonoBehaviour
{
    [Button]
    public async void SendScore(string leaderboardID, double score)
    {
        try
        {
            AddPlayerScoreOptions options = new AddPlayerScoreOptions();
            options.Metadata = "Img_20";

            LeaderboardEntry entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardID, score, options);
            print("id_(" + entry.PlayerId + ") Name: " + entry.PlayerName + " score: " + entry.Score);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [Button]
    public async void GetScore(string leaderboardID)
    {
        try
        {
            GetPlayerScoreOptions options = new GetPlayerScoreOptions();
            options.IncludeMetadata = true;

            LeaderboardEntry entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardID, options);
            print("id_(" + entry.PlayerId + ") Name: " + entry.PlayerName + " score: " + entry.Score + " Metadata:" + entry.Metadata.ToString());
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [Button]
    public async void GetTopScores(string leaderboardID)
    {
        GetScoresOptions options = new GetScoresOptions();
        options.Limit = 2;

        try
        {
            LeaderboardScoresPage page = await LeaderboardsService.Instance.GetScoresAsync(leaderboardID, options);

            foreach (LeaderboardEntry entry in page.Results)
            {
                print("id_(" + entry.PlayerId + ") Name: " + entry.PlayerName + " score: " + entry.Score + " Metadata:" + entry.Metadata.ToString());
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [Button]
    public async void GetRangeScores(string leaderboardID)
    {
        await LeaderboardsService.Instance.GetPlayerRangeAsync(leaderboardID);
    }
}
