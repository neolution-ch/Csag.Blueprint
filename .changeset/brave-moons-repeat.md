---
"@neolution-ch/csag-blueprint-web": patch
---

Make the OpenAPI Problem Details unification independent of endpoint compile order

Two CLR types reach the OpenAPI document under the same schema name:
`Microsoft.AspNetCore.Mvc.ProblemDetails`, which `ProblemDetailsOperationProcessor` attaches to every
operation, and FastEndpoints' own `ProblemDetails`, used for validation failures. NSwag gives one of
them the bare name and suffixes the other `ProblemDetails2`. Which one wins depends on the order the
schema generator first encounters them, which follows endpoint discovery order and therefore compile
order.

`UnifiedProblemDetailsDocumentProcessor` assumed a fixed winner. It removed `ProblemDetails2` after
rewriting references, but the rewrite only walked Paths → Operations → Responses → Content. A
reference reachable any other way survived and pointed at a definition that no longer existed, so
`OpenApiDocument.ToJson()` threw *"Could not find the JSON path of a referenced schema"*.

Because the trigger is compile order, this surfaced in consuming apps as a build that broke when an
endpoint folder was renamed — the build-time spec export failed with no obvious connection to the
rename.

The processor now redirects alias references with a `JsonReferenceVisitorBase` walk over the whole
document, so request bodies, parameters, nested properties, array items and composition keywords are
covered too, and it folds every `ProblemDetails<N>` alias rather than only `ProblemDetails2`. The
generated document is unchanged for consumers that were already working.
