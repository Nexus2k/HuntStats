﻿using System.Text.RegularExpressions;
using Dapper;
using Dommel;
using HuntStats.Data;
using HuntStats.Models;
using HuntStats.State;
using MediatR;
using Newtonsoft.Json;
using Entry = HuntStats.Models.Entry;

namespace HuntStats.Features;

public class RemoveMatchByIdCommand : IRequest<GeneralStatus>
{
    public RemoveMatchByIdCommand(int matchId)
    {
        MatchId = matchId;
    }

    public int MatchId { get; set; }
}

public class RemoveMatchByIdCommandHandler : IRequestHandler<RemoveMatchByIdCommand, GeneralStatus>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMediator _mediator;
    private readonly AppState _appState;

    public RemoveMatchByIdCommandHandler(IDbConnectionFactory connectionFactory, IMediator mediator, AppState appState)
    {
        _connectionFactory = connectionFactory;
        _mediator = mediator;
        _appState = appState;
    }

    public async Task<GeneralStatus> Handle(RemoveMatchByIdCommand request, CancellationToken cancellationToken)
    {
        using var con = await _connectionFactory.GetOpenConnectionAsync();
        await con.QueryAsync("DELETE FROM Match WHERE Id = @MatchId", new { MatchId = request.MatchId });
        await con.QueryAsync("DELETE FROM Entries WHERE MatchId = @MatchId", new { MatchId = request.MatchId });
        await con.QueryAsync("DELETE FROM Accolades WHERE MatchId = @MatchId", new { MatchId = request.MatchId });
        await con.QueryAsync("DELETE FROM Teams WHERE MatchId = @MatchId", new { MatchId = request.MatchId });
        _appState.MatchAdded();
        return GeneralStatus.Succes;
    }
}

public class GetMatchbyIdCommand : IRequest<HuntMatch>
{
    public GetMatchbyIdCommand(int id)
    {
        Id = id;
    }

    public int Id { get; set; }
}
public class GetMatchbyIdCommandHandler : IRequestHandler<GetMatchbyIdCommand, HuntMatch>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GetMatchbyIdCommandHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }


    public async Task<HuntMatch> Handle(GetMatchbyIdCommand request, CancellationToken cancellationToken)
    {
        using var con = await _connectionFactory.GetOpenConnectionAsync();

        var match = await con.SelectAsync<HuntMatchTable>(x => x.Id == request.Id);

        var mappedHuntMatch = match.Select(async x =>
        {
            var teams = await con.SelectAsync<TeamTable>(j => j.MatchId == x.Id);
            var huntMatch = new HuntMatch()
            {
                Id = x.Id,
                DateTime = x.DateTime,
                Teams = teams.Select(team => new Team()
                {
                    Id = team.Id,
                    Mmr = team.Mmr,
                    Players = JsonConvert.DeserializeObject<List<Player>>(team.Players)
                }).ToList()
            };
            return huntMatch;
        }).Select(x => x.Result);

        return mappedHuntMatch.FirstOrDefault();
    }
}

public class GetAccoladesByMatchIdCommand : IRequest<List<Accolade>> {

    public GetAccoladesByMatchIdCommand(int matchId)
    {
        MatchId = matchId;
    }

    public int MatchId { get; set; }
}

public class GetAccoladesByMatchIdCommandHandler : IRequestHandler<GetAccoladesByMatchIdCommand, List<Accolade>>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GetAccoladesByMatchIdCommandHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    public async Task<List<Accolade>> Handle(GetAccoladesByMatchIdCommand request, CancellationToken cancellationToken)
    {
        var con = await _connectionFactory.GetOpenConnectionAsync();
        var accolades = await con.SelectAsync<Accolade>(x => x.MatchId == request.MatchId);
        return accolades.ToList();
    }
}

public class GetEntriesByMatchIdCommand : IRequest<List<Entry>> {

    public GetEntriesByMatchIdCommand(int matchId)
    {
        MatchId = matchId;
    }

    public int MatchId { get; set; }
}

public class GetEntriesByMatchIdCommandHandler : IRequestHandler<GetEntriesByMatchIdCommand, List<Entry>>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GetEntriesByMatchIdCommandHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    public async Task<List<Entry>> Handle(GetEntriesByMatchIdCommand request, CancellationToken cancellationToken)
    {
        var con = await _connectionFactory.GetOpenConnectionAsync();
        var entries = await con.SelectAsync<Entry>(x => x.MatchId == request.MatchId);
        return entries.ToList();
    }
}



public class GetMatchCommand : IRequest<MatchView>
{
    public OrderType OrderType { get; set; } = OrderType.Descending;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class MatchView
{
    public int Total { get; set; }
    public List<HuntMatch> Matches { get; set; }
}

public class GetMatchCommandHandler : IRequestHandler<GetMatchCommand, MatchView>
{
    private readonly IDbConnectionFactory _connectionFactory;
    
    public GetMatchCommandHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    
    public async Task<MatchView> Handle(GetMatchCommand request, CancellationToken cancellationToken)
    {
        using var con = await _connectionFactory.GetOpenConnectionAsync();

        var matches = await con.GetAllAsync<HuntMatchTable>();

        var mappedHuntMatch = matches.Select(async x =>
        {
            var teams = await con.SelectAsync<TeamTable>(j => j.MatchId == x.Id);
            var huntMatch = new HuntMatch()
            {
                Id = x.Id,
                DateTime = x.DateTime,
                Teams = teams.Select(team => new Team()
                {
                    Id = team.Id,
                    Mmr = team.Mmr,
                    Players = JsonConvert.DeserializeObject<List<Player>>(team.Players)
                }).ToList()
            };
            huntMatch.TotalKills = huntMatch.Teams.Select(x => x.Players.Select(x => x.KilledByMe).Sum()).Sum();
            huntMatch.TotalKillsWithTeammate = huntMatch.TotalKills +
                                               huntMatch.Teams.Select(x =>
                                                   x.Players.Select(x => x.KilledByTeammate).Sum()).Sum();
            huntMatch.TotalDeaths = huntMatch.Teams.Select(x => x.Players.Select(x => x.KilledByMe).Sum()).Sum();

            return huntMatch;
        }).Select(x => x.Result);
        
        if(request.OrderType == OrderType.Descending) return new MatchView()
        {
            Total = mappedHuntMatch.Count(),
            Matches = mappedHuntMatch.OrderByDescending(x => x.DateTime).Skip((request.Page-1) * request.PageSize).Take(request.PageSize).ToList()
        };
        if(request.OrderType == OrderType.Ascending) return new MatchView()
        {
            Total = mappedHuntMatch.Count(),
            Matches = mappedHuntMatch.OrderBy(x => x.DateTime).Skip((request.Page-1) * request.PageSize).Take(request.PageSize).ToList()
        };
        return null;
    }
}

public class GetAllMatchCommand : IRequest<List<HuntMatch>>
{
    public OrderType OrderType { get; set; } = OrderType.Descending;
}
public class GetAllMatchCommandHandler : IRequestHandler<GetAllMatchCommand, List<HuntMatch>>
{
    private readonly IDbConnectionFactory _connectionFactory;
    
    public GetAllMatchCommandHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    
    public async Task<List<HuntMatch>> Handle(GetAllMatchCommand request, CancellationToken cancellationToken)
    {
        using var con = await _connectionFactory.GetOpenConnectionAsync();

        var matches = await con.GetAllAsync<HuntMatchTable>();

        var mappedHuntMatch = matches.Select(async x =>
        {
            var teams = await con.SelectAsync<TeamTable>(j => j.MatchId == x.Id);
            var huntMatch = new HuntMatch()
            {
                Id = x.Id,
                DateTime = x.DateTime,
                Teams = teams.Select(team => new Team()
                {
                    Id = team.Id,
                    Mmr = team.Mmr,
                    Players = JsonConvert.DeserializeObject<List<Player>>(team.Players)
                }).ToList()
            };
            huntMatch.TotalKills = huntMatch.Teams.Select(x => x.Players.Select(x => x.KilledByMe).Sum()).Sum();
            huntMatch.TotalKillsWithTeammate = huntMatch.TotalKills +
                                               huntMatch.Teams.Select(x =>
                                                   x.Players.Select(x => x.KilledByTeammate).Sum()).Sum();
            huntMatch.TotalDeaths = huntMatch.Teams.Select(x => x.Players.Select(x => x.KilledByMe).Sum()).Sum();

            return huntMatch;
        }).Select(x => x.Result);

        if (request.OrderType == OrderType.Descending)
            return mappedHuntMatch.OrderByDescending(x => x.DateTime).ToList();
        if (request.OrderType == OrderType.Ascending)
            return mappedHuntMatch.OrderBy(x => x.DateTime).ToList();
        return null;
    }
}