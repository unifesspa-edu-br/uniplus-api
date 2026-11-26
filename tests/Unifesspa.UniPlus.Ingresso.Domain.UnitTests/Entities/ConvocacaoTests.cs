namespace Unifesspa.UniPlus.Ingresso.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Enums;
using Unifesspa.UniPlus.Ingresso.Domain.Events;
using Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;
using Unifesspa.UniPlus.Ingresso.TestSupport.Cenarios;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

public sealed class ConvocacaoTests
{
    private static readonly DateTimeOffset Instante = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    private readonly Guid _chamadaId = Guid.NewGuid();
    private readonly Guid _inscricaoId = Guid.NewGuid();
    private readonly Guid _candidatoId = Guid.NewGuid();
    private readonly FakeTimeProvider _relogio = new(Instante);

    [Fact]
    public void Criar_MesmaOfertaEmDuasConvocacoes_CadaUmaGuardaCopiaPropria()
    {
        CopiaDaOfertaDeCurso oferta = new CopiaDaOfertaDeCursoDeTeste().Construir();
        Convocacao primeira = Convocacao.Criar(_chamadaId, _inscricaoId, _candidatoId,
            ProtocoloConvocacao.Gerar(1, 1, _relogio), 1, oferta, _relogio);
        Convocacao segunda = Convocacao.Criar(_chamadaId, Guid.NewGuid(), Guid.NewGuid(),
            ProtocoloConvocacao.Gerar(1, 2, _relogio), 2, oferta, _relogio);

        primeira.OfertaCurso.Should().NotBeSameAs(oferta).And.NotBeSameAs(segunda.OfertaCurso);
        segunda.OfertaCurso.Should().NotBeSameAs(oferta);
        primeira.OfertaCurso.Should().Be(oferta);
        segunda.OfertaCurso.Should().Be(oferta);
        primeira.OfertaCurso.Turnos.Should().Equal(oferta.Turnos);
    }

    [Fact(DisplayName = "Convocação criada nasce pendente com os dados informados")]
    public void Criar_ComDadosValidos_NasceComStatusPendenteEOsDadosInformados()
    {
        ProtocoloConvocacao protocolo = ProtocoloConvocacao.Gerar(1, 1, _relogio);

        Convocacao convocacao = Convocacao.Criar(_chamadaId, _inscricaoId, _candidatoId, protocolo, 3, new CopiaDaOfertaDeCursoDeTeste().Construir(), _relogio);

        convocacao.Status.Should().Be(StatusConvocacao.Pendente);
        convocacao.ChamadaId.Should().Be(_chamadaId);
        convocacao.InscricaoId.Should().Be(_inscricaoId);
        convocacao.CandidatoId.Should().Be(_candidatoId);
        convocacao.Protocolo.Should().Be(protocolo);
        convocacao.Posicao.Should().Be(3);
        convocacao.OfertaCurso.Should().Be(new CopiaDaOfertaDeCursoDeTeste().Construir());
        convocacao.DataManifestacao.Should().BeNull();
    }

    [Fact(DisplayName = "Convocação criada registra uma única vez o evento de candidato convocado, no instante do relógio")]
    public void Criar_ComDadosValidos_RegistraUmUnicoEventoDeCandidatoConvocado()
    {
        ProtocoloConvocacao protocolo = ProtocoloConvocacao.Gerar(1, 1, _relogio);

        Convocacao convocacao = Convocacao.Criar(_chamadaId, _inscricaoId, _candidatoId, protocolo, 3, new CopiaDaOfertaDeCursoDeTeste().Construir(), _relogio);

        CandidatoConvocadoEvent evento = convocacao.DomainEvents.Should().ContainSingle().Which
            .Should().BeOfType<CandidatoConvocadoEvent>().Subject;
        evento.InscricaoId.Should().Be(_inscricaoId);
        evento.CandidatoId.Should().Be(_candidatoId);
        evento.ChamadaId.Should().Be(_chamadaId);
        evento.Protocolo.Should().Be(protocolo.Valor);
        evento.OccurredOn.Should().Be(Instante);
    }

    [Fact(DisplayName = "Com protocolo e relógio nulos, a criação falha pelo protocolo")]
    public void Criar_ProtocoloNuloERelogioNulo_FalhaPeloProtocolo()
    {
        Action criar = () => Convocacao.Criar(_chamadaId, _inscricaoId, _candidatoId, null!, 3, new CopiaDaOfertaDeCursoDeTeste().Construir(), null!);

        criar.Should().Throw<ArgumentNullException>().WithParameterName("protocolo");
    }
    [Fact]
    public void Criar_CopiaNula_LancaArgumentNullException()
    {
        ProtocoloConvocacao protocolo = ProtocoloConvocacao.Gerar(1, 1, _relogio);
        Action criar = () => Convocacao.Criar(_chamadaId, _inscricaoId, _candidatoId, protocolo, 3, null!, _relogio);
        criar.Should().Throw<ArgumentNullException>().WithParameterName("ofertaCurso");
    }
}
