import Foundation

/// JSON coders matched to what the .NET backend actually emits.
public enum AtlasJSON {
    /// Decoder for API responses.
    public static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom(decodeDate)
        return decoder
    }()

    /// Encoder for API requests.
    public static let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .custom { date, encoder in
            var container = encoder.singleValueContainer()
            try container.encode(fractional.format(date))
        }
        return encoder
    }()

    // ASP.NET Core serialises DateTimeOffset with up to seven fractional
    // digits and an explicit offset ("2026-01-01T12:00:00.1234567+00:00"),
    // and omits the fraction entirely when it is zero. Foundation's built-in
    // `.iso8601` strategy rejects fractional seconds outright, so a perfectly
    // standard payload fails to decode with a misleading error.
    //
    // `Date.ISO8601FormatStyle` handles both shapes and - unlike
    // ISO8601DateFormatter - is Sendable, so these can safely be shared
    // `static let`s instead of being rebuilt per value or guarded by a lock.
    private static let fractional = Date.ISO8601FormatStyle(includingFractionalSeconds: true)
    private static let plain = Date.ISO8601FormatStyle(includingFractionalSeconds: false)

    private static func decodeDate(_ decoder: Decoder) throws -> Date {
        let container = try decoder.singleValueContainer()
        let raw = try container.decode(String.self)

        if let date = try? fractional.parse(raw) {
            return date
        }
        if let date = try? plain.parse(raw) {
            return date
        }

        throw DecodingError.dataCorruptedError(
            in: container,
            debugDescription: "Expected an ISO 8601 date, got '\(raw)'."
        )
    }
}
