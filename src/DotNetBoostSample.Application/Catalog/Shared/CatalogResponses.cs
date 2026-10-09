namespace DotNetBoostSample.Application.Catalog.Shared;

/// <summary>
/// What a create answers with: the id, and a localized sentence naming what was created.
///
/// <para>A 201 is the one write whose body already had something the caller needs, so the sentence
/// is added beside the id rather than in place of it.</para>
/// </summary>
public sealed record CreatedResponse(int Id, string Message);

/// <summary>
/// What an update or a delete answers with — which is why they return <c>200 OK</c> rather than
/// <c>204 No Content</c>: a 204 is defined to have no body, so it has nowhere to put the sentence.
///
/// <para>The caller is free to ignore it, but a caller that wants to say what happened no longer
/// has to write the sentence itself, in the one language its own bundle happens to be in.</para>
/// </summary>
public sealed record MessageResponse(string Message);

/// <summary>
/// What an upload answers with. Named after the file rather than the product's SKU: the file is
/// the thing that just arrived, and it is the only one of the two the uploader can get wrong.
/// </summary>
public sealed record UploadedImageResponse(Guid Id, string FileName, long Size, string Message);
