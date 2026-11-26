namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using System.Text.Json;

using Domain.Entities;
using Domain.Errors;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Compõe as recusas independentes do envio sobre o preenchimento final, antes de qualquer gravação.</summary>
public sealed class ConferenciaDoEnvioDaInscricao(
    ConferenciaDoEnderecoDaInscricao enderecos,
    ConferenciaDosMunicipiosDaInscricao municipios,
    DocumentosEnviadosDaInscricao documentos,
    IResolvedorFusoInstitucional fuso,
    IUserContext usuario)
{
    public async Task<Result<EnvioConferido>> ConferirAsync(EnvioPreparado pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        CorpoDoRascunho mesclado = Mesclar(pedido);
        List<FieldError> recusas = [.. PreenchimentoDoCandidato.RecusasDeForma(pedido.Recebido.Respostas, pedido.Recebido.Grupos, pedido.Contexto.Admitidas), .. pedido.Inscricao.Formulario.Preenchimento.ComRespostas(mesclado.Respostas).RecusasDoEndereco(pedido.Contexto.Admitidas)];
        if (recusas.Any(static r => r.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado))
        {
            return Result<EnvioConferido>.ValidationFailure(recusas);
        }

        HashSet<string> camposRecusados = [.. recusas.Select(static r => r.Field).OfType<string>()];
        return await ConferirAvaliacaoAsync(pedido, mesclado, recusas, camposRecusados, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<EnvioConferido>> ConferirAvaliacaoAsync(
        EnvioPreparado pedido, CorpoDoRascunho mesclado, List<FieldError> recusas, HashSet<string> camposRecusados, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, JsonElement> conferiveis = mesclado.Respostas.Where(r => !camposRecusados.Contains(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);
        ConferenciaDosEnderecos endereco = await enderecos.ConferirAsync(conferiveis, pedido.Contexto.Admitidas, aoEnviar: true, cancellationToken).ConfigureAwait(false);
        recusas.AddRange(endereco.Recusas);
        DerivadosCalculados derivados = DerivadosDoSistemaInscricao.Calcular(mesclado.Respostas, pedido.Contexto.DataReferenciaFatos, endereco);
        Result<TimeZoneInfo> resolvido = fuso.Resolver();
        return resolvido.IsFailure
            ? Result<EnvioConferido>.Failure(resolvido.Error!)
            : await AvaliarAsync(new AvaliacaoDoEnvio(pedido, mesclado, endereco, derivados, new EnvioDasRespostas(pedido.Instante, resolvido.Value!)),
                recusas, camposRecusados, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<EnvioConferido>> AvaliarAsync(
        AvaliacaoDoEnvio entrada, List<FieldError> recusas, HashSet<string> camposRecusados, CancellationToken cancellationToken)
    {
        ArquivosDaInscricao arquivos = await documentos.CarregarAsync(entrada.Pedido.Inscricao.Id, cancellationToken).ConfigureAwait(false);
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = AvaliacaoDaInscricaoPorOpcao.Avaliar(entrada.Pedido.Contexto, EntradaDaAvaliacaoDaInscricao.Montar(
            DerivadosDoSistemaInscricao.ComOsDerivados(entrada.Pedido.Contexto.Admitidas.Definicoes), new EntradaDaAvaliacaoDaInscricao(entrada.Mesclado.Respostas,
            InscricaoRequestMapper.ParaAvaliacao(entrada.Mesclado.Grupos), FatosConhecidos(entrada.Derivados),
            AvaliacaoDaInscricao.CodigosDasPaginasDaInscricao(entrada.Pedido.Contexto.Configuracao.Definicao),
            entrada.Envio, arquivos.ApresentacoesVerificadas(), NaFaseDaInscricao: true)));
        return avaliacao.IsFailure
            ? Result<EnvioConferido>.Failure(avaliacao.Error!)
            : await ConferirMunicipiosAsync(entrada, arquivos, avaliacao.Value!, recusas, camposRecusados, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<EnvioConferido>> ConferirMunicipiosAsync(
        AvaliacaoDoEnvio entrada, ArquivosDaInscricao arquivos, ResultadoDaAvaliacaoDaInscricao avaliacao,
        List<FieldError> recusas, HashSet<string> camposRecusados, CancellationToken cancellationToken)
    {
        ResultadoDaConferenciaDosMunicipios conferidos = await municipios.ConferirAsync(
            entrada.Pedido.Contexto.Configuracao.Definicao, entrada.Pedido.Contexto.Admitidas, avaliacao.Formulario,
            entrada.Mesclado.Respostas, entrada.Mesclado.Grupos, camposRecusados, cancellationToken).ConfigureAwait(false);
        RespostasAdmitidas admitidas = entrada.Pedido.Contexto.Admitidas with { NomeCivilDoLogin = usuario.NomeCompleto, NomeSocialDoLogin = usuario.NomeSocial };
        recusas.AddRange(RecusasFinais(entrada, arquivos, avaliacao, conferidos, admitidas));
        if (recusas.Count > 0)
        {
            return Result<EnvioConferido>.ValidationFailure([.. recusas.Distinct()]);
        }
        return (entrada.Endereco.GeoIndisponivel ?? conferidos.GeoIndisponivel) is { } indisponivel
            ? Result<EnvioConferido>.Failure(indisponivel)
            : Result<EnvioConferido>.Success(new EnvioConferido(entrada, arquivos, avaliacao, conferidos, admitidas));
    }

    private static IEnumerable<FieldError> RecusasFinais(
        AvaliacaoDoEnvio entrada, ArquivosDaInscricao arquivos, ResultadoDaAvaliacaoDaInscricao avaliacao, ResultadoDaConferenciaDosMunicipios municipiosConferidos, RespostasAdmitidas admitidas)
    {
        EnvioPreparado pedido = entrada.Pedido;
        DependenciaDoGeo? dependencia = entrada.Endereco.GeoIndisponivel is null ? null
            : new DependenciaDoGeo(pedido.Contexto.Configuracao.Definicao, entrada.Derivados.Indeterminados, CaminhosRespondidos(entrada.Mesclado));
        OpcoesDeCursoDaInscricao opcoes = OpcoesDeCursoDaInscricao.Reidratar(pedido.Request.OfertaCursoId, pedido.Request.OfertaCursoSegundaOpcaoId);
        return pedido.Inscricao.RecusasDoEnvio(PreenchimentoDoCandidato.ParaValidacao(opcoes, pedido.Recebido.Respostas, pedido.Recebido.Grupos, pedido.Recebido.Aceites), admitidas,
                new ApuracaoDoEnvio(avaliacao, entrada.Derivados.Valores, DerivadosDoSistemaInscricao.DefinicoesDasRespostas, []), arquivos.EmVerificacao())
            .Concat(ModalidadesDoEnvio.DaAvaliacao(dependencia?.SemAsModalidadesDoGeo(avaliacao,
                pedido.Contexto.ConfiguracaoDaPrimeiraOpcao, pedido.Contexto.ConfiguracaoDaSegundaOpcao) ?? avaliacao, opcoes).Recusas(opcoes, admitidas))
            .Concat(RecusasDoFormulario(entrada, avaliacao, municipiosConferidos, admitidas, dependencia))
            .Concat(municipiosConferidos.Recusas);
    }

    private static IEnumerable<FieldError> RecusasDoFormulario(
        AvaliacaoDoEnvio entrada, ResultadoDaAvaliacaoDaInscricao avaliacao, ResultadoDaConferenciaDosMunicipios municipiosConferidos,
        RespostasAdmitidas admitidas, DependenciaDoGeo? dependencia)
    {
        Dictionary<string, JsonElement> finais = RespostasComDerivados(entrada);
        bool nomeSocialRecusado = ConferenciaDosNomesDoTitular.RecusasDoNomeSocialAoEnviar(entrada.Mesclado.Respostas, admitidas).Any();
        HashSet<string> camposDoGeo = [.. municipiosConferidos.Recusas.Select(static r => r.Field).OfType<string>()];
        return VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(finais, entrada.Mesclado.Grupos, entrada.Pedido.Contexto.Ofertas)
            .Concat(VeredictoDeSubmissao.AvaliarDistinguindoOGeo(AvaliacaoDaInscricao.DaInscricao(avaliacao.Formulario),
                    admitidas.Termos, entrada.Pedido.Recebido.Aceites, entrada.Envio.Dia, dependencia)
                .Where(static r => !r.DependeDoGeo).Select(static r => r.Recusa)
                .Where(e => !nomeSocialRecusado || !string.Equals(e.Field, ConjuntoBasicoDaInscricao.NomeSocial, StringComparison.Ordinal))
                .Where(e => e.Field is null || !camposDoGeo.Contains(e.Field)));
    }

    private static Dictionary<string, JsonElement> RespostasComDerivados(AvaliacaoDoEnvio entrada)
    {
        Dictionary<string, JsonElement> finais = new(entrada.Mesclado.Respostas, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in entrada.Derivados.Valores)
        {
            finais[codigo] = valor;
        }
        return finais;
    }

    private static CorpoDoRascunho Mesclar(EnvioPreparado pedido) => new(
        new Dictionary<string, JsonElement>(pedido.Inscricao.Formulario.Preenchimento.RespostasAposMesclar(pedido.Recebido.Respostas), StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(pedido.Inscricao.Formulario.Preenchimento.GruposAposMesclar(pedido.Recebido.Grupos), StringComparer.Ordinal),
        pedido.Recebido.Aceites);

    private static Dictionary<string, FatoResolvido> FatosConhecidos(DerivadosCalculados derivados)
    {
        Dictionary<string, FatoResolvido> fatos = derivados.Valores.ToDictionary(static d => d.Key, static d => FatoResolvido.Resolvido(d.Value), StringComparer.Ordinal);
        foreach (string indeterminado in derivados.Indeterminados)
        {
            fatos[indeterminado] = FatoResolvido.Indeterminado();
        }
        return fatos;
    }

    private static HashSet<string> CaminhosRespondidos(CorpoDoRascunho preenchimento) =>
    [
        .. preenchimento.Respostas.Where(static r => !RespostaDeCampo.EstaVazia(r.Value)).Select(static r => r.Key),
        .. preenchimento.Grupos.SelectMany(static g => g.Value.SelectMany(o => o.Respostas
            .Where(static r => !RespostaDeCampo.EstaVazia(r.Value)).Select(r => $"{g.Key}.{o.Id}.{r.Key}"))),
    ];
}

/// <summary>O preenchimento mesclado e os fatos usados para avaliar o envio, no mesmo dia institucional.</summary>
public sealed record AvaliacaoDoEnvio(
    EnvioPreparado Pedido, CorpoDoRascunho Mesclado, ConferenciaDosEnderecos Endereco, DerivadosCalculados Derivados, EnvioDasRespostas Envio);

/// <summary>O envio sem recusas, com os documentos e os municípios conferidos para registrar a evidência.</summary>
public sealed record EnvioConferido(
    AvaliacaoDoEnvio Entrada, ArquivosDaInscricao Arquivos, ResultadoDaAvaliacaoDaInscricao Avaliacao, ResultadoDaConferenciaDosMunicipios Municipios, RespostasAdmitidas Admitidas);
