using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Collections.Generic;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    public class ControllerBase : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        protected virtual ApiResult<T> ApiResult<T>(T data)
        {
            return new ApiResult<T>
            {
                Code = 200,
                Data = data
            };
        }

        protected virtual ApiResult ApiResult(int statusCode, string message)
        {
            if (Response != null)
            {
                Response.StatusCode = statusCode;
            }
            return new ApiResult
            {
                Code = statusCode,
                Message = message
            };
        }

        protected virtual ApiResult<T> ApiResult<T>(int statusCode, string message)
        {
            if (Response != null)
            {
                Response.StatusCode = statusCode;
            }
            return new ApiResult<T>
            {
                Code = statusCode,
                Message = message
            };
        }

        protected virtual ApiPagedResult<T> Paged<T>(
            int statusCode,
            string message)
        {
            return new ApiPagedResult<T>
            {
                Code = statusCode,
                Message = message
            };
        }

        protected virtual async ValueTask<ApiPagedResult<T>> PagedAsync<T>(
            IQueryable<T> src,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var totalRows = await src.CountAsync(cancellationToken);
            var totalPages = (totalRows + pageSize - 1) / pageSize;
            var p = 0;
            if (Request.Query.ContainsKey("p"))
            {
                int.TryParse(Request.Query["p"], out p);
            }
            return new ApiPagedResult<T>
            {
                Code = 200,
                PageSize = pageSize,
                Current = p,
                TotalRows = totalRows,
                TotalPages = totalPages,
                Data = await (src
                    .Skip(pageSize * p)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken))
            };
        }

        protected ApiPagedResult<T> Paged<T>(
            IEnumerable<T> src,
            int pageSize)
        {
            var totalRows = src.Count();
            var totalPages = (totalRows + pageSize - 1) / pageSize;
            var p = 0;
            if (Request.Query.ContainsKey("p"))
            {
                int.TryParse(Request.Query["p"], out p);
            }
            return new ApiPagedResult<T>
            {
                Code = 200,
                PageSize = pageSize,
                Current = p,
                TotalRows = totalRows,
                TotalPages = totalPages,
                Data = src.Skip(pageSize * p).Take(pageSize).ToList()
            };
        }

        protected virtual async ValueTask<ApiPagedResult<T2>> PagedAsync<T1, T2>(
            IQueryable<T1> src,
            int pageSize,
            Func<IEnumerable<T1>, IEnumerable<T2>> func,
            CancellationToken cancellationToken = default)
        {
            var totalRows = await src.CountAsync(cancellationToken);
            var totalPages = (totalRows + pageSize - 1) / pageSize;
            var p = 0;
            if (Request.Query.ContainsKey("p"))
            {
                int.TryParse(Request.Query["p"], out p);
            }
            return new ApiPagedResult<T2>
            {
                Code = 200,
                PageSize = pageSize,
                Current = p,
                TotalRows = totalRows,
                TotalPages = totalPages,
                Data = func(await (src
                    .Skip(pageSize * p)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken)))
            };
        }
    }
}
