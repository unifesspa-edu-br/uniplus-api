namespace Unifesspa.UniPlus.Ingresso.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Events;
using Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;
using Unifesspa.UniPlus.Ingresso.TestSupport.Cenarios;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

public sealed class MatriculaTests
{
    private static readonly DateTimeOffset Instante = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_CopiaDaConvocacao_PreservaOferta()
    {
        FakeTimeProvider relogio = new(Instante);
        Convocacao convocacao = Convocacao.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ProtocoloConvocacao.Gerar(1, 1, relogio), 1, new CopiaDaOfertaDeCursoDeTeste().Construir(), relogio);
        Matricula matricula = Matricula.Criar(convocacao.Id, convocacao.CandidatoId, convocacao.OfertaCurso);
        Matricula outra = Matricula.Criar(convocacao.Id, convocacao.CandidatoId, convocacao.OfertaCurso);
        matricula.OfertaCurso.Should().NotBeSameAs(convocacao.OfertaCurso).And.NotBeSameAs(outra.OfertaCurso);
        matricula.OfertaCurso.Should().Be(convocacao.OfertaCurso);
        matricula.OfertaCurso.Turnos.Should().Equal(convocacao.OfertaCurso.Turnos);
    }

    [Fact]
    public void Efetivar_Matricula_RegistraCodigoDaOfertaNoEvento()
    {
        Matricula matricula = Matricula.Criar(Guid.NewGuid(), Guid.NewGuid(), new CopiaDaOfertaDeCursoDeTeste().Construir());
        matricula.Efetivar(new FakeTimeProvider(Instante));
        MatriculaEfetivadaEvent evento = matricula.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<MatriculaEfetivadaEvent>().Subject;
        evento.OfertaCursoCodigo.Should().Be("ENGENHARIA_CIVIL_MARABA");
        evento.MatriculaId.Should().Be(matricula.Id);
        evento.CandidatoId.Should().Be(matricula.CandidatoId);
        evento.OccurredOn.Should().Be(Instante);
    }

    [Fact]
    public void Criar_CopiaNula_LancaArgumentNullException()
    {
        Action criar = () => Matricula.Criar(Guid.NewGuid(), Guid.NewGuid(), null!);
        criar.Should().Throw<ArgumentNullException>().WithParameterName("ofertaCurso");
    }
}
