namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class ConferenciaDosNomesDoTitular
{
    internal static IEnumerable<FieldError> RecusasDoNome(IReadOnlyDictionary<string, JsonElement> respostas, RespostasAdmitidas admitidas)
    {
        if (!admitidas.Definicoes.ContainsKey(ConjuntoBasicoDaInscricao.Nome))
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(admitidas.NomeCivilDoLogin))
        {
            return [new FieldError(ConjuntoBasicoDaInscricao.Nome, new DomainError(InscricaoErrorCodes.NomeAusenteNoLogin,
                "O login não traz o seu nome completo. Entre de novo ou procure o suporte para corrigir o cadastro da conta."))];
        }

        return RecusasDoNomeInformado(respostas, admitidas.NomeCivilDoLogin);
    }

    private static IEnumerable<FieldError> RecusasDoNomeInformado(IReadOnlyDictionary<string, JsonElement> respostas, string nomeDoLogin)
    {
        if (!respostas.TryGetValue(ConjuntoBasicoDaInscricao.Nome, out JsonElement valor) || valor.ValueKind == JsonValueKind.Null
            || ValorIgualAoNome(valor, nomeDoLogin))
        {
            return [];
        }

        return [new FieldError(ConjuntoBasicoDaInscricao.Nome, new DomainError(InscricaoErrorCodes.NomeDiferenteDoLogin,
            "O nome informado tem de ser o da sua conta. Para alterá-lo, corrija o cadastro da conta."))];
    }

    public static IEnumerable<FieldError> RecusasDoNomeSocialAoSalvar(IReadOnlyDictionary<string, JsonElement> respostas, RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(admitidas);
        if (!DeveConferirNomeSocial(respostas, admitidas) || !TemNomeSocial(respostas, out JsonElement valor))
        {
            return [];
        }

        return RecusasDoNomeSocialInformado(valor, admitidas.NomeSocialDoLogin);
    }

    public static IEnumerable<FieldError> RecusasDoNomeSocialAoEnviar(IReadOnlyDictionary<string, JsonElement> respostas, RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(admitidas);
        if (!DeveConferirNomeSocial(respostas, admitidas))
        {
            return [];
        }

        bool informado = TemNomeSocial(respostas, out JsonElement valor);
        return string.IsNullOrWhiteSpace(admitidas.NomeSocialDoLogin) ? [RecusaDoNomeSocialAusente()]
            : informado ? RecusasDoNomeSocialInformado(valor, admitidas.NomeSocialDoLogin) : [];
    }

    private static bool DeveConferirNomeSocial(IReadOnlyDictionary<string, JsonElement> respostas, RespostasAdmitidas admitidas) =>
        admitidas.Definicoes.ContainsKey(ConjuntoBasicoDaInscricao.NomeSocial)
        && respostas.TryGetValue(ConjuntoBasicoDaInscricao.DesejaNomeSocial, out JsonElement deseja)
        && deseja.ValueKind == JsonValueKind.True;

    private static bool TemNomeSocial(IReadOnlyDictionary<string, JsonElement> respostas, out JsonElement valor) =>
        respostas.TryGetValue(ConjuntoBasicoDaInscricao.NomeSocial, out valor)
        && valor.ValueKind != JsonValueKind.Null
        && !(valor.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(valor.GetString()));

    private static IEnumerable<FieldError> RecusasDoNomeSocialInformado(JsonElement valor, string? nomeDaConta)
    {
        if (string.IsNullOrWhiteSpace(nomeDaConta))
        {
            return [RecusaDoNomeSocialAusente()];
        }

        return ValorIgualAoNome(valor, nomeDaConta) ? [] : [new FieldError(ConjuntoBasicoDaInscricao.NomeSocial,
            new DomainError(InscricaoErrorCodes.NomeSocialDiferenteDaConta, "O nome social informado tem de ser o da sua conta. Para cadastrar ou alterar, use Meu perfil."))];
    }

    private static FieldError RecusaDoNomeSocialAusente() => new(ConjuntoBasicoDaInscricao.NomeSocial,
        new DomainError(InscricaoErrorCodes.NomeSocialAusenteNaConta,
            "Você marcou que deseja usar nome social, mas sua conta não tem nome social cadastrado. Cadastre o nome social em Meu perfil ou desmarque a opção."));

    private static bool ValorIgualAoNome(JsonElement valor, string nome) => valor.ValueKind == JsonValueKind.String
        && NomeNormalizado(valor.GetString()) == NomeNormalizado(nome);

    private static string NomeNormalizado(string? nome) =>
        string.Join(' ', (nome ?? string.Empty).Normalize(System.Text.NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
