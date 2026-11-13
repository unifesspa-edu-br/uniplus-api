namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Monta a vista da configuração de um processo em rascunho sem coleta nenhuma, para testar uma
/// regra isolada sem precisar do agregado: o teste parte do estado vazio e declara só o que a
/// regra lê.
/// </summary>
internal sealed class EstadoDaConfiguracaoBuilder
{
    private EstadoDaConfiguracao _estado = EstadoDaConfiguracao.De(NovoProcesso());

    internal static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2026 — SiSU",
        TipoProcesso.SiSU,
        OrigemCandidatos.InscricaoPropria,
        Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        IdentificadoresDeTeste.Novo());

    internal EstadoDaConfiguracaoBuilder ComEtapas(params EtapaProcesso[] etapas)
    {
        _estado = _estado with { Etapas = etapas };
        return this;
    }

    internal EstadoDaConfiguracaoBuilder ComClassificacao(ConfiguracaoClassificacao classificacao)
    {
        _estado = _estado with { Classificacao = classificacao };
        return this;
    }

    internal EstadoDaConfiguracaoBuilder ComCriteriosDeDesempate(params CriterioDesempate[] criterios)
    {
        _estado = _estado with { CriteriosDesempate = criterios };
        return this;
    }

    internal EstadoDaConfiguracaoBuilder Publicado()
    {
        _estado = _estado with { Status = StatusProcesso.Publicado };
        return this;
    }

    internal EstadoDaConfiguracao Construir() => _estado;
}
