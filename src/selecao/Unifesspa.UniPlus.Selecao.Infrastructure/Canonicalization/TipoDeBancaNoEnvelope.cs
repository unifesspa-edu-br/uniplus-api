namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A cópia do tipo de banca que a banca da fase e a da etapa guardam, no envelope: o tipo de origem, o código, o nome, o papel,
/// as duas marcas e os tipos de decisão que o tipo julga. Os códigos são os de contrato dos vocabulários; a lista segue a
/// ordem alfabética ordinal do código, como a dos tipos de decisão da fase e da etapa.
/// </summary>
internal static class TipoDeBancaNoEnvelope
{
    private const string CampoDosTiposDeDecisao = "tiposDeDecisaoJulgados";

    /// <summary>As chaves que a cópia do tipo escreve em cada banca, todas obrigatórias na leitura.</summary>
    internal static readonly string[] Chaves =
    [
        "tipoBancaOrigemId", "codigo", "nome", "papel", "exigeDuasOuMaisBancas", "encerraRecursoNaPrimeiraInstancia", CampoDosTiposDeDecisao,
    ];

    internal static void Serializar(JsonObject banca, TipoDeBancaCopiado tipo)
    {
        banca["tipoBancaOrigemId"] = tipo.OrigemId;
        banca["codigo"] = tipo.Codigo.Codigo();
        banca["nome"] = HashCanonicalComputer.NormalizeNfc(tipo.Nome);
        banca["papel"] = tipo.Papel.Codigo();
        banca["exigeDuasOuMaisBancas"] = tipo.ExigeDuasOuMaisBancas;
        banca["encerraRecursoNaPrimeiraInstancia"] = tipo.EncerraRecursoNaPrimeiraInstancia;
        banca[CampoDosTiposDeDecisao] = TiposDeDecisaoNoEnvelope.Serializar(tipo.TiposDeDecisaoJulgados);
        TiposDeDecisaoNoEnvelope.AcrescentarContexto(banca, CampoDosTiposDeDecisao, tipo.TiposDeDecisaoJulgados);
        ContextoDosRotulos.Acrescentar(banca, "codigo", tipo.Codigo);
        ContextoDosRotulos.Acrescentar(banca, "papel", tipo.Papel);
    }

    internal static TipoDeBancaCopiado? Ler(LeitorEnvelope leitor, JsonObject banca, string path)
    {
        Guid origemId = leitor.Identificador(banca, "tipoBancaOrigemId", path);
        TipoDeBanca codigo = leitor.CodigoDeContrato<TipoDeBanca>(banca, "codigo", path);
        string nome = leitor.TextoNaoVazio(banca, "nome", path, LimitesDoEnvelope.TipoBancaNome);
        PapelDaBanca papel = leitor.CodigoDeContrato<PapelDaBanca>(banca, "papel", path);
        bool exigeDuasOuMais = leitor.Booleano(banca, "exigeDuasOuMaisBancas", path);
        bool encerraNaPrimeira = leitor.Booleano(banca, "encerraRecursoNaPrimeiraInstancia", path);
        IReadOnlyList<string> tipos = leitor.Falhou ? [] : TiposDeDecisaoNoEnvelope.Ler(leitor, banca, path, CampoDosTiposDeDecisao);

        return leitor.Falhou
            ? null
            : new TipoDeBancaCopiado(origemId, codigo, nome, papel, exigeDuasOuMais, encerraNaPrimeira, TiposDeDecisaoDeclarados.Canonicos(tipos));
    }
}
