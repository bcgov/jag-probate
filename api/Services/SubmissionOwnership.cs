using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Probate.Api.Infrastructure.Authentication;
using Probate.Db.Models;

namespace Probate.Api.Services;

/// <summary>
/// Shared database ownership rules. CreatedByUserId is authoritative; deprecated
/// CreatedBy may match only unclaimed rows. All reads and writes claim legacy rows.
/// Remove the username fallback only after remaining legacy rows have been claimed.
/// </summary>
public static class SubmissionOwnership
{
    /// <summary>
    /// Exact owner-ID match, or a nonblank legacy username match with a null owner ID.
    /// Never load a submission by public ID before applying this predicate.
    /// </summary>
    public static Expression<Func<Submission, bool>> OwnedBy(SubmissionOwner owner) =>
        s =>
            s.CreatedByUserId == owner.UserId
            || (
                s.CreatedByUserId == null
                && owner.LegacyUsername != null
                && s.CreatedBy == owner.LegacyUsername
            );

    /// <summary>
    /// Lists owned live rows in one query, then claims unclaimed legacy rows in one
    /// SaveChanges batch. Each UPDATE keeps the CreatedByUserId IS NULL and exact
    /// CreatedBy guard. If a concurrent claim wins, the batch rolls back and the list
    /// is reloaded under the shared predicate, so rows claimed by another ID drop out.
    /// </summary>
    public static async Task<List<Submission>> ListAndClaimAsync(
        ProbateDbContext db,
        SubmissionOwner owner,
        CancellationToken cancellationToken = default
    )
    {
        var query = db
            .Submissions.Where(OwnedBy(owner))
            .Where(s => s.DeletedAt == null)
            .OrderByDescending(s => s.CreatedAt);
        var rows = await query.ToListAsync(cancellationToken);
        var legacy = rows.Where(s => s.CreatedByUserId == null).ToList();
        if (legacy.Count == 0)
            return rows;

        foreach (var submission in legacy)
            submission.CreatedByUserId = owner.UserId;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return rows;
        }
        catch (DbUpdateConcurrencyException)
        {
            foreach (var submission in rows)
                db.Entry(submission).State = EntityState.Detached;
            return await query.AsNoTracking().ToListAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Finds an owned row and atomically claims it before any read or write.
    /// EF concurrency tokens add original CreatedByUserId IS NULL and the exact
    /// legacy CreatedBy to the claim UPDATE's WHERE clause. CreatedBy is not changed.
    /// A concurrent claim by another ID yields no owned row; a same-ID claim may proceed.
    /// Deleted rows are excluded except for the delete service's idempotent fallback.
    /// </summary>
    public static async Task<Submission?> FindAndClaimAsync(
        ProbateDbContext db,
        SubmissionOwner owner,
        Expression<Func<Submission, bool>> selector,
        CancellationToken cancellationToken = default,
        bool includeDeleted = false
    )
    {
        var query = db.Submissions.Where(OwnedBy(owner)).Where(selector);
        if (!includeDeleted)
            query = query.Where(s => s.DeletedAt == null);

        var submission = await query
            .OrderBy(s => s.DeletedAt != null)
            .FirstOrDefaultAsync(cancellationToken);
        if (submission == null || submission.CreatedByUserId != null)
            return submission;

        submission.CreatedByUserId = owner.UserId;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return submission;
        }
        catch (DbUpdateConcurrencyException ex)
            when (ex.Entries.Count == 1 && ReferenceEquals(ex.Entries[0].Entity, submission))
        {
            // Discard the losing claim; requery only this row under authoritative ownership.
            db.Entry(submission).State = EntityState.Detached;
            return await query
                .Where(s => s.Id == submission.Id && s.CreatedByUserId == owner.UserId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
