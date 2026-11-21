import Foundation
import Testing
@testable import AtlasNetworking

@Suite("Date coding against real .NET payloads")
struct AtlasJSONTests {
    private struct Wrapper: Codable, Equatable {
        let createdAt: Date
    }

    /// Exactly the shapes System.Text.Json produces for DateTimeOffset.
    @Test("Decodes every shape ASP.NET Core emits", arguments: [
        "2026-01-01T12:00:00+00:00",
        "2026-01-01T12:00:00.1234567+00:00",
        "2026-01-01T12:00:00.123+02:00",
        "2026-01-01T12:00:00Z",
        "2026-01-01T12:00:00.1234567Z",
        "2026-01-01T12:00:00.5-05:00",
    ])
    func decodesDotNetDateShapes(raw: String) throws {
        let json = Data(#"{"createdAt":"\#(raw)"}"#.utf8)

        let decoded = try AtlasJSON.decoder.decode(Wrapper.self, from: json)

        #expect(decoded.createdAt.timeIntervalSince1970 > 1_700_000_000)
    }

    @Test("Offsets are honoured rather than ignored")
    func offsetsAreHonoured() throws {
        let utc = try AtlasJSON.decoder.decode(
            Wrapper.self, from: Data(#"{"createdAt":"2026-01-01T12:00:00Z"}"#.utf8)
        )
        let plusTwo = try AtlasJSON.decoder.decode(
            Wrapper.self, from: Data(#"{"createdAt":"2026-01-01T12:00:00+02:00"}"#.utf8)
        )

        // Noon at +02:00 is two hours earlier in absolute terms than noon UTC.
        #expect(utc.createdAt.timeIntervalSince(plusTwo.createdAt) == 7200)
    }

    @Test("A non-date string fails loudly instead of defaulting")
    func rejectsGarbage() {
        let json = Data(#"{"createdAt":"last Tuesday"}"#.utf8)

        #expect(throws: DecodingError.self) {
            try AtlasJSON.decoder.decode(Wrapper.self, from: json)
        }
    }

    @Test("Encode then decode round-trips to the same instant")
    func roundTrips() throws {
        let original = Wrapper(createdAt: Date(timeIntervalSince1970: 1_800_000_000.123))

        let data = try AtlasJSON.encoder.encode(original)
        let decoded = try AtlasJSON.decoder.decode(Wrapper.self, from: data)

        #expect(abs(decoded.createdAt.timeIntervalSince(original.createdAt)) < 0.001)
    }
}

@Suite("Problem details")
struct ProblemDetailsTests {
    @Test("Decodes the backend's conflict response, including the extensions")
    func decodesConflict() throws {
        let json = Data("""
        {
          "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
          "title": "Conflict",
          "status": 409,
          "detail": "That email address is already registered.",
          "code": "identity.user.email_taken",
          "traceId": "0af7651916cd43dd8448eb211c80319c"
        }
        """.utf8)

        let problem = try AtlasJSON.decoder.decode(ProblemDetails.self, from: json)

        #expect(problem.status == 409)
        #expect(problem.code == "identity.user.email_taken")
        #expect(problem.traceId == "0af7651916cd43dd8448eb211c80319c")
    }

    @Test("Decodes a validation response's per-field errors")
    func decodesValidationErrors() throws {
        let json = Data("""
        {
          "title": "Validation",
          "status": 400,
          "errors": { "Email": ["'Email' is not a valid email address."] }
        }
        """.utf8)

        let problem = try AtlasJSON.decoder.decode(ProblemDetails.self, from: json)

        #expect(problem.errors?["Email"]?.count == 1)
    }
}
