using Movies.Application.Models;

namespace Movies.Application.Services;

public interface IRatingService
{
    Task<bool> RateMovieAsync(Guid movieId, int rating, Guid userId, CancellationToken token);
    Task<bool> DeleteRatingAsync(Guid movieId, Guid userId, CancellationToken token);
    Task<IEnumerable<MovieRating>> GetRatingForUserAsync(Guid userId, CancellationToken token);

}