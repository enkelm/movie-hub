using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Movies.Api.Mapping;
using Movies.Application.Services;
using Movies.Contracts.Requests;
using Movies.Contracts.Responses;

namespace Movies.Api.Controllers;

[ApiVersion(1.0)]
[ApiController]
public class MoviesController : ControllerBase
{
    private readonly IMovieService _movieService;
    private readonly IOutputCacheStore _outputCacheStore;

    public MoviesController(IMovieService movieService, IOutputCacheStore outputCacheStore)
    {
        _movieService = movieService;
        _outputCacheStore = outputCacheStore;
    }

    [Authorize(AuthConstants.TrustedMemberPolicyName)]
    [HttpPost(ApiEndpoints.Movies.Create)]
    [ProducesResponseType(typeof(MovieResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationFailureResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody]CreateMovieRequest request, CancellationToken token)
    {
        var movie = request.MapToMovie();
        await _movieService.CreateAsync(movie, token);
        await _outputCacheStore.EvictByTagAsync("movies", token);
        return CreatedAtAction(nameof(Get), new { idOrSlug = movie.Id }, movie);
    }

    [HttpGet(ApiEndpoints.Movies.Get)]
    [OutputCache(PolicyName = "MovieCache")]
    // [ResponseCache(Duration = 30, VaryByHeader = "Accept, Accept-Encoding", Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(typeof(MovieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] string idOrSlug, [FromServices] LinkGenerator linkGenerator, CancellationToken token)
    {
        var userId = HttpContext.GetUserId();
        var movie = Guid.TryParse(idOrSlug, out var id) ? 
                await _movieService.GetByIdAsync(id, userId, token):
                await _movieService.GetBySlugAsync(idOrSlug, userId, token);
        
        if (movie is null) return NotFound();
        
        var response = movie.MapToResponse();

        var movieObj = new { id = movie.Id };
        
        response.Links.Add(new Link
        {
            Href = linkGenerator.GetPathByAction(HttpContext, nameof(Get), values: new {idOrSlug = movie.Id}),
            Rel = "self",
            Type = "GET"
        });
        
        response.Links.Add(new Link
        {
            Href = linkGenerator.GetPathByAction(HttpContext, nameof(Update), values: movieObj),
            Rel = "self",
            Type = "PUT"
        });
        
        response.Links.Add(new Link
        {
            Href = linkGenerator.GetPathByAction(HttpContext, nameof(Delete), values: movieObj),
            Rel = "self",
            Type = "DELETE"
        });

        return Ok(response);
    }

    [HttpGet(ApiEndpoints.Movies.GetAll)]
    [OutputCache(PolicyName = "MovieCache")]
    // [ResponseCache(Duration = 30, VaryByQueryKeys = new []{"title", "year", "sortBy", "page", "pageSize"})]
    [ProducesResponseType(typeof(MoviesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] GetAllMoviesRequest request, CancellationToken token)
    {
        var userId = HttpContext.GetUserId();
        var options = request.MapTopOptions().WithUser(userId);
        var movies = await _movieService.GetAllAsync(options, token);
        var movieCount = await _movieService.GetCountAsync(options.Title, options.YearOfRelease, token);
        var response = movies.MapToResponse(request.PageSize, request.PageSize, movieCount);
        return Ok(response);
    }

    [Authorize(AuthConstants.TrustedMemberPolicyName)]
    [HttpPut(ApiEndpoints.Movies.Update)]
    [ProducesResponseType(typeof(MovieResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationFailureResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateMovieRequest request, CancellationToken token)
    {
        var userId = HttpContext.GetUserId();
        var movie = request.MapToMovie(id);
        var updatedMovie = await _movieService.UpdateAsync(movie, userId, token);
        if (updatedMovie is null) return NotFound();
        var response = updatedMovie.MapToResponse();
        await _outputCacheStore.EvictByTagAsync("movies", token);
        return Ok(response);
    }

    [Authorize(AuthConstants.AdminUserPolicyName)]
    [HttpDelete(ApiEndpoints.Movies.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken token)
    {
        var deleted = await _movieService.DeleteByIdAsync(id, token);
        if (!deleted) return NotFound();
        await _outputCacheStore.EvictByTagAsync("movies", token);
        return Ok();
    }
}

