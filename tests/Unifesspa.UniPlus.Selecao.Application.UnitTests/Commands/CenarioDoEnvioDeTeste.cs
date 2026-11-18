namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;

internal sealed class CenarioDoEnvioDeTeste
{
    public CenarioDoEnvioDeTeste(Cpf cpf, (VersaoConfiguracao Versao, ConfiguracaoPublicada Configuracao, VersaoTermoEscolhida Termo) configuracao)
    {
        Cpf = cpf;
        (Versao, Configuracao, Termo) = configuracao;
        Inscricao = SubmeterDeTeste.NovaInscricao(cpf, "conta-1", Versao.Id);
        (Inscricoes, Processos, Codecs) = SubmeterDeTeste.PortasPara(Inscricao, Versao, Configuracao);
    }

    public Cpf Cpf { get; }
    public VersaoConfiguracao Versao { get; }
    public ConfiguracaoPublicada Configuracao { get; }
    public VersaoTermoEscolhida Termo { get; }
    public Inscricao Inscricao { get; }
    public IInscricaoRepository Inscricoes { get; }
    public IComprovanteDaInscricaoRepository Comprovantes => SubmeterDeTeste.ComprovantesDe(Inscricoes);
    public IProcessoSeletivoRepository Processos { get; }
    public IRegistroCodecsEnvelope Codecs { get; }
    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
    public InscricaoRequest Pedido => RequestComApelidoETermoAceito(Cpf, Termo, Configuracao);
    public Task<Result<ReciboDaInscricaoDto>> SubmeterAsync(
        InscricaoRequest? pedido = null, IGeradorDeNumeroInscricao? numeros = null, TimeProvider? relogio = null, IConferenciaEnderecoGeo? geo = null) =>
        new SubmissaoDeTeste(Inscricoes, Processos, Codecs, UnitOfWork, SubmeterDeTeste.UsuarioComCpf(Cpf))
        {
            Geo = geo ?? SubmeterDeTeste.ConferenciaQueAceitaTudo(),
            Numeros = numeros ?? SubmeterDeTeste.GeradorQueDevolve("00000001"),
            Relogio = relogio ?? DivulgacaoDeTeste.Relogio,
        }.SubmeterAsync(new SubmeterInscricaoCommand(ProcessoId, pedido ?? Pedido, PrecondicaoIfMatch.DeTags([Inscricao.ETag])));
}
