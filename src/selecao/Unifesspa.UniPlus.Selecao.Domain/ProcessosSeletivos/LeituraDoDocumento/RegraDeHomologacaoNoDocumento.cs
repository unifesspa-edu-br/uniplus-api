namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>
/// A regra de homologação das inscrições como o documento a traz, copiada sem conferir regra: o resultado que publica a lista e as
/// alternativas de condições. O motivo de cada condição vai pelo código, e a leitura não o resolve.
/// </summary>
public sealed record RegraDeHomologacaoNoDocumento(string? ResultadoAtoCodigo, IReadOnlyList<AlternativaNoDocumento> Alternativas);

/// <summary>Um grupo de condições que valem juntas.</summary>
public sealed record AlternativaNoDocumento(IReadOnlyList<CondicaoNoDocumento> Condicoes);

/// <summary>
/// Uma condição de homologação como o documento a traz: o tipo (a Application traduz o código, que o esquema fecha em uma enumeração),
/// a edição do exame, o código do motivo desfavorável e o enquadramento em JSON, que a leitura monta pelo domínio do predicado.
/// </summary>
public sealed record CondicaoNoDocumento(TipoDeCondicaoDeHomologacao? Tipo, int? EdicaoDoExame, string? MotivoDesfavoravelCodigo, JsonElement? Enquadramento);
