namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A inscrição na visão de quem opera o processo, montada do que se leu e do que a pessoa pode ver: o campo fixo, pela
/// permissão da propriedade, e o item de resposta, de derivado e de arquivo, pela permissão da classe, não saem quando a
/// pessoa não os pode ver, sem indicação.
/// </summary>
public static class MontadorDaInscricaoAdministrativa
{
    public static InscricaoAdministrativaDto Montar(
        ProcessoDaInscricaoDto processo, ConteudoDaInscricao conteudo, ProjecaoDaInscricao projecao, bool podeEmitirComprovante)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(projecao);

        CamposFixosVisiveis campos = CamposFixosVisiveis.De(conteudo, projecao);
        ItensVisiveis itens = ItensVisiveis.De(conteudo, projecao);

        return new InscricaoAdministrativaDto(
            conteudo.Inscricao.Id, processo, campos.Situacao, campos.Ofertas, campos.NumeroInscricao, campos.SubmetidaEm, campos.CanceladaEm, campos.MotivoDoCancelamento,
            itens.Secoes, itens.Derivados, itens.Arquivos, podeEmitirComprovante, conteudo.NumeroDoUltimoComprovante);
    }

    private sealed record CamposFixosVisiveis(
        SituacaoInscricao? Situacao,
        IReadOnlyList<OfertaEscolhidaDto>? Ofertas,
        string? NumeroInscricao,
        DateTimeOffset? SubmetidaEm,
        DateTimeOffset? CanceladaEm,
        string? MotivoDoCancelamento)
    {
        public static CamposFixosVisiveis De(ConteudoDaInscricao conteudo, ProjecaoDaInscricao projecao)
        {
            Inscricao inscricao = conteudo.Inscricao;

            return new CamposFixosVisiveis(
                projecao.Fixo<SituacaoInscricao>(nameof(InscricaoAdministrativaDto.Situacao), inscricao.Situacao),
                projecao.Permite(nameof(InscricaoAdministrativaDto.Ofertas)) ? OfertasDaInscricao(inscricao, conteudo.Envelope) : null,
                projecao.Fixo(nameof(InscricaoAdministrativaDto.NumeroInscricao), inscricao.NumeroInscricao),
                conteudo.PrimeiroEnvio,
                projecao.Fixo<DateTimeOffset>(nameof(InscricaoAdministrativaDto.CanceladaEm), inscricao.CanceladaEm),
                projecao.Fixo(nameof(InscricaoAdministrativaDto.MotivoDoCancelamento), inscricao.MotivoDoCancelamento));
        }

        private static IReadOnlyList<OfertaEscolhidaDto> OfertasDaInscricao(Inscricao inscricao, EnvelopeReidratado envelope) =>
            OfertasEscolhidas.Montar(
                inscricao.OfertaCursoId,
                inscricao.OfertaCursoSegundaOpcaoId,
                OfertasEscolhidas.NomesDe(ContextoDaVersaoFixadaResolver.OfertasDaVersao(envelope.Grafo)));
    }

    private sealed record ItensVisiveis(
        IReadOnlyList<SecaoDaInscricaoDto> Secoes,
        IReadOnlyList<ItemDeRespostaDto> Derivados,
        IReadOnlyList<ArquivoAdministrativoDto> Arquivos)
    {
        public static ItensVisiveis De(ConteudoDaInscricao conteudo, ProjecaoDaInscricao projecao)
        {
            Inscricao inscricao = conteudo.Inscricao;
            FatosDaVersao fatos = FatosDaVersao.De(conteudo.Envelope);

            return new ItensVisiveis(
                ItensDaInscricao.Permitidas(ItensDaInscricao.Secoes(inscricao.Respostas, inscricao.Grupos, fatos, projecao.PodeRevelar), projecao.Decisoes),
                ItensDaInscricao.Visiveis(ItensDaInscricao.Derivados(inscricao.Derivados, fatos), projecao.Decisoes),
                [.. conteudo.Arquivos.Where(projecao.Decisoes.ItemSai)]);
        }
    }
}
