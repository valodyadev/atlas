import Foundation

/// RFC 9457 `application/problem+json`, as returned by the Atlas API.
///
/// Mirrors the shape produced by `Atlas.ServiceDefaults.ErrorHandling`, so the
/// stable `code` is available to the client and the `traceId` can be shown in
/// a support screen and looked up in the trace backend.
public struct ProblemDetails: Decodable, Sendable, Equatable {
    public let title: String?
    public let detail: String?
    public let status: Int?

    /// Stable, dotted error code, e.g. `identity.user.email_taken`.
    public let code: String?

    /// Distributed-trace identifier for this request.
    public let traceId: String?

    /// Per-field validation messages, when the status is 400.
    public let errors: [String: [String]]?

    public init(
        title: String? = nil,
        detail: String? = nil,
        status: Int? = nil,
        code: String? = nil,
        traceId: String? = nil,
        errors: [String: [String]]? = nil
    ) {
        self.title = title
        self.detail = detail
        self.status = status
        self.code = code
        self.traceId = traceId
        self.errors = errors
    }
}
