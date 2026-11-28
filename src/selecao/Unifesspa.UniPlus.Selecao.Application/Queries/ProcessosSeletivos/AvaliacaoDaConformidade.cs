namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;
using Domain.ProcessosSeletivos.Bancas;
using Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Domain.ValueObjects;

using DTOs;

using Microsoft.Extensions.Options;

using Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// Avalia o checklist de conformidade de um processo seletivo: lê o que o agregado não tem e projeta o
/// resultado de <see cref="ChecklistDeConformidade"/> para o DTO público. O checklist em si vive no Domain,
/// coincide com a conferência da configuração que <c>Publicar</c> e <c>Retificar</c> aplicam: a fonte
/// única evita que a leitura pública e a publicação divirjam.
/// </summary>
/// <remarks>
/// <para>
/// <b>O contexto que o agregado não tem.</b> O calendário vigente é do módulo Configuração, e o reconhecimento do
/// fuso institucional depende da base de fusos do runtime. As duas leituras são resolvidas aqui e passadas
/// prontas, porque o Domain não lê reader nem serviço (ADR-0042). Sem elas, o checklist teria de inferir o que
/// não sabe, e devolveria conforme para processo que a publicação recusa.
/// </para>
/// <para>
/// <b>Corretores.</b> O mínimo de integrantes das bancas depende do número de corretores que a regra de consolidação de
/// cada etapa exige. O painel o lê pelo mesmo serviço que o congelamento usa e mostra a pendência que a publicação
/// recusaria.
/// </para>
/// <para>
/// <b>Avisos.</b> Os avisos vêm de <c>ChecklistDeConformidade.AvaliarAvisos</c> e viajam em lista própria: só a
/// consulta de conformidade os calcula, nunca a conferência que congela a versão.
/// </para>
/// </remarks>
public sealed class AvaliacaoDaConformidade(
    LeituraDaVersaoPublicadaDoPainel versaoPublicada,
    LeituraDosCadastrosDoPainel cadastros,
    ConferenciaDoCatalogoEDaProtecao catalogos,
    IOptions<ProvaPresencialOptions> options)
{
    /// <summary>
    /// Item do checklist para a versão publicada que o codec não lê: sem ela não há classificação
    /// congelada para conferir, e a retificação recusaria o processo pelo mesmo motivo.
    /// </summary>
    internal const string CodigoDaVersaoPublicadaIlegivel = "cronograma_versao_publicada_legivel";

    internal async Task<ConformidadeProcessoSeletivoDto> AvaliarAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        VersaoPublicadaNoPainel publicada = await versaoPublicada.LerAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        LeiturasDoPainel leituras = await cadastros.LerAsync(processo, publicada, cancellationToken).ConfigureAwait(false);
        CatalogoVivo catalogo = await catalogos.LerAsync(processo, cancellationToken).ConfigureAwait(false);

        return Projetar(processo, leituras, catalogo, publicada);
    }

    private ConformidadeProcessoSeletivoDto Projetar(
        ProcessoSeletivo processo, LeiturasDoPainel leituras, CatalogoVivo catalogo, VersaoPublicadaNoPainel publicada)
    {
        ItemConformidadeDto[] itens =
        [
            .. ItensDoChecklist(processo, leituras, catalogo, publicada),
            .. ConferenciaDaAprovacaoDaProtecao.Conferir(processo, catalogo.TiposVivos, catalogo.Fatos).Errors.Select(ItemDaProtecaoPendente),
            .. ItemDaVersaoIlegivel(publicada.Falha),
        ];
        AvisoConformidadeDto[] avisos = [.. ChecklistDeConformidade.AvaliarAvisos(processo.Declaracao(), leituras.TiposDeAtoUnicos.Nomes)
            .Select(static aviso => new AvisoConformidadeDto(aviso.Codigo, aviso.Dimensao, aviso.Mensagem))];

        return new ConformidadeProcessoSeletivoDto(processo.Id, itens, avisos);
    }

    // A mesma proteção e a mesma aprovação que a publicação e a retificação avaliam, sobre a mesma leitura dos tipos de
    // documento: o painel não aprova o que elas recusam.
    private IEnumerable<ItemConformidadeDto> ItensDoChecklist(
        ProcessoSeletivo processo, LeiturasDoPainel leituras, CatalogoVivo catalogo, VersaoPublicadaNoPainel publicada)
    {
        ProtecaoVigenteDasExigencias protecao = ProtecaoVigenteDasExigenciasDoProcesso
            .Montar(processo, catalogo.TiposVivos, catalogo.Fatos, publicada.CongeladoNaVersao).Exigencias;

        CadastrosDaConfiguracao conferidos = new(
            leituras.Contexto, catalogo.FatosDeModalidade, leituras.TiposDeAtoUnicos, leituras.Regras.Corretores, protecao, leituras.MotivosEscolhidos)
        { Regras = leituras.Regras.Regras, NomesDasFases = catalogo.NomesDasFases, MunicipiosDoGeo = leituras.MunicipiosDoGeo, LimiteDeMunicipiosDeProva = options.Value.LimiteDeMunicipios, Precedencias = leituras.Precedencias };

        return ChecklistDeConformidade
            .AvaliarConformidade(processo.Declaracao(), conferidos)
            .Select(static item => new ItemConformidadeDto(item.Codigo, item.Dimensao, item.Mensagem, item.Ok));
    }

    // O código do item é único no checklist: leva o alvo (o tipo ou o fato), porque vários itens pendentes dividem a
    // mesma invariante.
    private static ItemConformidadeDto ItemDaProtecaoPendente(FieldError erro) =>
        new(
            $"{erro.Error.Code}:{erro.Field}",
            erro.Field?.StartsWith("fatos.", StringComparison.Ordinal) == true ? DimensaoConformidade.ColetaDeFatos : DimensaoConformidade.ExigenciasDocumentais,
            erro.Error.Message,
            Ok: false);

    private static IEnumerable<ItemConformidadeDto> ItemDaVersaoIlegivel(DomainError? falha) =>
        falha is null
            ? []
            : [new ItemConformidadeDto(CodigoDaVersaoPublicadaIlegivel, DimensaoConformidade.Cronograma, falha.Message, Ok: false)];
}
