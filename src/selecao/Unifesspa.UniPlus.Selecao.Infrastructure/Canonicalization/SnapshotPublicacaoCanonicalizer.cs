namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeção canônica do envelope de congelamento (ADR-0100, ADR-0109): projeta a configuração viva do
/// agregado num payload de blocos nomeados e devolve os bytes pelo <see cref="PerfilCanonicoV1"/>.
/// Cada bloco é montado por uma classe <c>Serializacao*</c> do seu assunto.
/// </summary>
/// <remarks>
/// <para>
/// O perfil fecha a forma dos bytes: reordena as chaves, normaliza em NFC toda string de negócio e
/// serializa de modo estável. Este tipo só monta o <see cref="JsonObject"/>.
/// </para>
/// <para>
/// A projeção é pura: mesma entrada, mesmos bytes. Tudo o que o envelope precisa além do agregado entra
/// por <see cref="EntradaCanonicalizacao"/>, montada pelo handler (ADR-0109 D6).
/// </para>
/// <para>
/// Coleções filhas chegam sem ordem garantida do banco, então todo bloco baseado em coleção é ordenado
/// antes de serializar: primeiro pela ordem semântica do campo, depois pela identidade de negócio e,
/// onde nenhuma das duas decide sozinha, pelo conteúdo canônico dos itens (ADR-0109 D9).
/// </para>
/// </remarks>
public sealed class SnapshotPublicacaoCanonicalizer : ISnapshotPublicacaoCanonicalizer
{
    /// <summary>
    /// Versão da forma do envelope (ADR-0109 D1). O codec é um só, reescrito no lugar quando a forma muda.
    /// </summary>
    internal const string SchemaVersionAtual = "0.0.43";

    /// <summary>
    /// Projeta o processo seletivo e os dados do edital no envelope canônico.
    /// </summary>
    /// <param name="entrada">A configuração viva e os dados congelados que o envelope precisa.</param>
    /// <returns>Os bytes canônicos, a versão do schema e o algoritmo do perfil.</returns>
    public SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        if (string.IsNullOrWhiteSpace(entrada.HashDocumento))
        {
            throw new ArgumentException("A entrada da canonicalização precisa do hash do documento.", nameof(entrada));
        }

        JsonObject payload = [];
        ProjetarEdital(payload, entrada);
        ProjetarVagas(payload, entrada.Processo);
        ProjetarClassificacao(payload, entrada.Processo);
        ProjetarExigenciasEFormularios(payload, entrada);
        ProjetarFatos(payload, entrada);

        byte[] bytes = EscritaCanonica.PerfilAtual.Serializar(payload);
        return new SnapshotCanonico(bytes, SchemaVersionAtual, EscritaCanonica.PerfilAtual.Algoritmo);
    }

    private static void ProjetarEdital(JsonObject payload, EntradaCanonicalizacao entrada)
    {
        ProcessoSeletivo processo = entrada.Processo;
        payload["tipoProcesso"] = SerializarTipoProcesso(processo);
        payload["periodo"] = SerializarPeriodo(entrada.Dados);
        payload["hashesEdital"] = SerializacaoDoEdital.SerializarHashesEdital(entrada.Dados, entrada.HashDocumento);
        payload["identificadorLegivel"] = processo.IdentificadorLegivel?.Valor;
        payload["apresentacaoDosPressupostos"] = SerializacaoDoEdital.SerializarApresentacaoDosPressupostos(entrada.ApresentacaoDosPressupostos);
        payload["tiposDeAto"] = SerializacaoDoEdital.SerializarTiposDeAto(entrada.TiposDeAtoCongelados);
        payload["localidade"] = SerializacaoDoEdital.SerializarLocalidade(processo, entrada.FusoHorario);
        payload["algoritmoContagemPrazo"] = SerializacaoDoEdital.SerializarAlgoritmoContagemPrazo(processo);
        payload["calendarioDiasUteis"] = SerializacaoDoEdital.SerializarCalendarioDiasUteis(entrada.CalendarioDiasUteis);
        payload["identidadesUnidade"] = SerializacaoDoEdital.SerializarIdentidadesUnidade(processo);
        ProjetarRetificacao(payload, entrada.Retificacao);
    }

    private static void ProjetarRetificacao(JsonObject payload, RetificacaoDoEdital? retificacao)
    {
        if (retificacao is null)
        {
            return;
        }

        payload["retificacao"] = new JsonObject
        {
            ["editalRetificadoId"] = retificacao.EditalRetificadoId,
            ["motivo"] = HashCanonicalComputer.NormalizeNfc(retificacao.Motivo),
        };
    }

    private static void ProjetarVagas(JsonObject payload, ProcessoSeletivo processo)
    {
        payload["vagas"] = SerializacaoDasVagas.SerializarVagas(processo);
        payload["distribuicao"] = SerializacaoDasVagas.SerializarDistribuicao(processo);
        payload["modalidades"] = SerializacaoDasVagas.SerializarModalidades(processo);
        payload["ofertas"] = SerializacaoDasVagas.SerializarOfertas(processo);
        payload["opcoesDeCurso"] = SerializacaoDasVagas.SerializarOpcoesDeCurso(processo);
        payload["atendimento"] = SerializacaoDoBonusEDoAtendimento.SerializarAtendimento(processo);
        payload["bonusRegional"] = SerializacaoDoBonusEDoAtendimento.SerializarBonusRegional(processo);
        payload["cascataRemanejamento"] = SerializacaoDaCascata.SerializarCascataRemanejamento(processo);
        payload["modalidadesOfertadas"] = SerializacaoDosFatos.SerializarModalidadesOfertadas(processo.DistribuicaoVagas);
    }

    private static void ProjetarClassificacao(JsonObject payload, ProcessoSeletivo processo)
    {
        payload["etapas"] = SerializacaoDasEtapas.SerializarEtapas(processo);
        payload["criteriosDesempate"] = SerializacaoDaClassificacao.SerializarCriteriosDesempate(processo);
        payload["classificacao"] = SerializacaoDaClassificacao.SerializarClassificacao(processo);
        payload["cronogramaFases"] = SerializacaoDoCronograma.SerializarCronogramaFases(processo);
    }

    private static void ProjetarExigenciasEFormularios(JsonObject payload, EntradaCanonicalizacao entrada)
    {
        ProcessoSeletivo processo = entrada.Processo;
        TimeZoneInfo fusoDaVersao = TimeZoneInfo.FindSystemTimeZoneById(entrada.FusoHorario);
        payload["documentosExigidos"] = SerializacaoDasExigencias.SerializarDocumentosExigidos(processo, entrada.Conformidade, entrada.MetadadosFatosCongelados, fusoDaVersao);
        payload["arvoreSatisfacao"] = SerializacaoDasExigencias.SerializarArvoreSatisfacao(processo);
        payload["formularios"] = SerializacaoDosFormularios.SerializarFormularios(processo);
        payload["divulgacao"] = SerializacaoDaDivulgacaoEDaTaxa.SerializarDivulgacao(processo);
        payload["taxaInscricao"] = SerializacaoDaDivulgacaoEDaTaxa.SerializarTaxaInscricao(processo);
    }

    private static void ProjetarFatos(JsonObject payload, EntradaCanonicalizacao entrada)
    {
        ProcessoSeletivo processo = entrada.Processo;
        payload["fatosColetados"] = SerializacaoDosFatos.SerializarFatosColetados(processo.FatosColetados, entrada.ValoresSelecionaveisCongelados);
        payload["gruposColetados"] = SerializacaoDosFatos.SerializarGruposColetados(processo.GruposColetados, entrada.ValoresSelecionaveisCongelados, entrada.AgregadosDosGrupos);
        payload["regrasDerivacao"] = SerializacaoDosFatos.SerializarRegrasDerivacao(processo.RegrasDerivacao);
        payload["grafoDependencia"] = SerializacaoDosFatos.SerializarGrafoDependencia(processo);
        payload["versaoInterpretador"] = MotorDerivacao.VersaoSemantica;
        payload["fatosDerivados"] = SerializacaoDosFatos.SerializarFatosDerivados(entrada.FatosDerivadosCongelados);
    }

    private static JsonObject SerializarTipoProcesso(ProcessoSeletivo processo) => new()
    {
        ["origemId"] = processo.TipoProcessoOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(processo.TipoProcesso.Codigo),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(processo.TipoProcesso.Nome),
    };

    private static JsonObject SerializarPeriodo(DadosEdital dados) => new()
    {
        ["numero"] = dados.Numero is { } numero ? HashCanonicalComputer.NormalizeNfc(numero) : null,
        ["inicio"] = HashCanonicalComputer.SerializeInstantCanonical(dados.PeriodoInscricaoInicio),
        ["fim"] = HashCanonicalComputer.SerializeInstantCanonical(dados.PeriodoInscricaoFim),
    };
}
