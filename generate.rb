def all()
    model = Sketchup.active_model()
    model.selection().clear()
    ents = []
    for e in model.entities()
        ents.push( e )
    end
    model.selection().add( ents )
end # of all()
def del()
    model = Sketchup.active_model()
    sel = model.selection()
    ents = model.entities()
    
    sel_ents = []
    for e in sel do sel_ents.push( e ) end
    
    ents.erase_entities( sel_ents )
end # of del()
def rounding(float,precision)
    return ((float * 10**precision).round.to_f) / (10**precision)
end
model=Sketchup.active_model
entities = model.entities
all()
a_coords = Array.new 
a_record = Array.new 
selection = model.selection

selection.each { |myedges|
    if myedges.typename == 'Edge'
       vertices = myedges.vertices    
       x_coord=rounding(vertices[0].position.x * 25.4,3)
       y_coord=rounding(vertices[0].position.y * 25.4,3) 
       a_record.clear
       a_record.push x_coord
       a_record.push y_coord
       a_coords.push a_record.dup
       x_coord=rounding(vertices[1].position.x * 25.4,3)
       y_coord=rounding(vertices[1].position.y * 25.4,3) 
       a_record.clear
       a_record.push x_coord
       a_record.push y_coord
       a_coords.push a_record.dup
    end
  }
a_coords.uniq!
a_coords.sort!
UI.messagebox(a_coords.inspect)
fname="c:\\Dropzone\\curve.txt"
fileout = File.open(fname, "w")
putstr="0 0"
fileout.puts putstr
a_coords.each{|x|
   putstr=x[0].to_s+" "+x[1].to_s
   fileout.puts putstr
   }
fileout.close
UI.messagebox(fname+" written")